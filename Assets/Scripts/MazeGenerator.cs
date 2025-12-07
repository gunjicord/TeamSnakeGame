using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

//maze generator using multi-agent Kruskal's algorithm (Union-Find)

public class MazeGenerator : MonoBehaviour
{
    public static MazeGenerator Instance { get; private set; }

    [Header("Maze Size (odd numbers recommended)")]
    public int width = 21;
    public int height = 21;
    public float cellSize = 1f;

    [Header("Prefabs")]
    public GameObject floorPrefab;
    public GameObject wallPrefab;
    public GameObject goalPrefab;

    [Header("Parent For Spawned Tiles")]
    public Transform mazeParent;

    [Header("Agents & Generation Settings")]
    [Range(1, 10)]
    public int agentCount = 3;
    public bool animateGeneration = true;
    [Tooltip("Time between carve steps when animateGeneration = true")]
    public float generationStepDelay = 0.01f;

    [Header("Difficulty (Loops / Dead Ends)")]
    [Range(0f, 1f)]
    public float difficulty = 1f;   

    [Header("Start / Goal Cells (grid coords)")]
    public Vector2Int startCell = new Vector2Int(1, 1);
    public Vector2Int goalCell;

    [Header("Settings UI")]
    public Canvas generationUI;
    public TextMeshProUGUI widthText;
    public TextMeshProUGUI heightText;
    public TextMeshProUGUI difficultyText;

    
    private int[,] grid;
    public int[,] Grid => grid;

    
    private Dictionary<Vector2Int, GameObject> floorTiles = new Dictionary<Vector2Int, GameObject>();
    private List<GameObject> wallTiles = new List<GameObject>();
    private GameObject goalInstance;

    public bool IsGenerating { get; private set; } = false;

    private Coroutine generationCoroutine;

    //Kruskal Union-Find state
    private int cellsX;            
    private int cellsY;             
    private int cellCount;           

    private int[] ufParent;
    private int[] ufRank;

    //edge two cells find closest
    private struct KruskalEdge
    {
        public Vector2Int cellA;     
        public Vector2Int cellB;    
        public Vector2Int wall;      
        public int agentIndex;       // which agent owns this edge
    }

    #region UNITY LIFECYCLE

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        widthText.text = "Width: " + width;
        heightText.text = "Height: " + height;
        difficultyText.text = "Difficulty: " + difficulty;
        //SetGeneration();
        //GenerateNewMaze();
    }

    #endregion

    #region PUBLIC API

    public void SetMazeWidth(float value)
    {
        width = (int)value;
        widthText.text = "Width: " + width;
    }

    public void SetMazeHeight(float value)
    {
        height = (int)value;
        heightText.text = "Height: " + height;
    }

    public void SetMazeDifficulty(float value)
    {
        difficulty = value;
        difficultyText.text = "Difficulty: " + difficulty;
    }

    public void StartGeneration()
    {
        generationUI.enabled = false;
        GenerateNewMaze();
    }

    public void GenerateNewMaze()
    {
        if (generationCoroutine != null)
            StopCoroutine(generationCoroutine);

        if (animateGeneration)
        {
            generationCoroutine = StartCoroutine(GenerateMazeCoroutine());
        }
        else
        {
            IsGenerating = true;
            ClearOldMazeVisual();
            NormalizeDimensions();
            InitializeGridAndCells();

            //pure logical generation
            GenerateMazeKruskalMultiAgent();             //maze
            AddExtraConnectionsBasedOnDifficulty(false); //add loops according to difficulty

            //build visuals from final grid using the prefabs
            BuildFullMazeVisual();

#if UNITY_EDITOR
            if (!HasPath(startCell, goalCell))
            {
                Debug.LogError("MazeGenerator: Non-animated maze ended up unsolvable, which should not happen.");
            }
#endif

            IsGenerating = false;
            generationCoroutine = null;
        }
    }

    //used by snakes and pathfinding
    public bool IsWalkable(Vector2Int cell)
    {
        if (grid == null) return false;
        if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height) return false;
        return grid[cell.x, cell.y] == 1;
    }

    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize, 0f, cell.y * cellSize);
    }

    public Vector2Int WorldToCell(Vector3 worldPos)
    {
        int x = Mathf.RoundToInt(worldPos.x / cellSize);
        int y = Mathf.RoundToInt(worldPos.z / cellSize);
        return new Vector2Int(x, y);
    }

    #endregion

    #region MAZE GENERATION (COROUTINE / ANIMATED)
    //animation for the maze, can be turned off if taking too long
    private IEnumerator GenerateMazeCoroutine()
    {
        IsGenerating = true;
        ClearOldMazeVisual();
        NormalizeDimensions();
        InitializeGridAndCells();

        
        yield return StartCoroutine(GenerateMazeKruskalMultiAgentAnimated());

        
        AddExtraConnectionsBasedOnDifficulty(true);

        
        BuildRemainingWallsAndGoalOnly();

#if UNITY_EDITOR
        if (!HasPath(startCell, goalCell))
        {
            Debug.LogError("MazeGenerator: Animated maze ended up unsolvable, which should not happen.");
        }
#endif

        IsGenerating = false;
        generationCoroutine = null;
    }

    #endregion

    #region CORE MAZE LOGIC (KRUSKAL + UNION-FIND)

    //make sure its odd
    private void NormalizeDimensions()
    {
        if (width < 5) width = 5;
        if (height < 5) height = 5;
        if (width % 2 == 0) width++;
        if (height % 2 == 0) height++;

        
        startCell = new Vector2Int(1, 1);
       
        goalCell = new Vector2Int(width - 2, height - 2);
    }

    
    private void InitializeGridAndCells()
    {
        grid = new int[width, height];

        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                grid[x, y] = 0;
        }

       
        cellsX = (width - 1) / 2;
        cellsY = (height - 1) / 2;
        cellCount = cellsX * cellsY;

        
        ufParent = new int[cellCount];
        ufRank = new int[cellCount];
        for (int i = 0; i < cellCount; i++)
        {
            ufParent[i] = i;
            ufRank[i] = 0;
        }

        //carve floor at all logical cell positions
        for (int cx = 0; cx < cellsX; cx++)
        {
            for (int cy = 0; cy < cellsY; cy++)
            {
                Vector2Int cellPos = CellIndexToGrid(cx, cy);
                grid[cellPos.x, cellPos.y] = 1; //floor i think
            }
        }
    }

    //no animations
    private void GenerateMazeKruskalMultiAgent()
    {
        List<KruskalEdge> allEdges = BuildKruskalEdges();
        System.Random rng = new System.Random();
        ShuffleList(allEdges, rng);

        //Assign edges 
        int agents = Mathf.Max(1, agentCount);
        List<KruskalEdge>[] edgesPerAgent = new List<KruskalEdge>[agents];
        for (int i = 0; i < agents; i++)
            edgesPerAgent[i] = new List<KruskalEdge>();

        for (int i = 0; i < allEdges.Count; i++)
        {
            KruskalEdge e = allEdges[i];
            int idx = i % agents;
            e.agentIndex = idx;
            edgesPerAgent[idx].Add(e);
        }

        //process agents in rounds, each trying to carve its edges
        bool edgesRemaining = true;

        while (edgesRemaining)
        {
            edgesRemaining = false;

            for (int agent = 0; agent < agents; agent++)
            {
                List<KruskalEdge> list = edgesPerAgent[agent];

                for (int i = 0; i < list.Count; i++)
                {
                    KruskalEdge e = list[i];
                    if (e.agentIndex == -1) continue; 

                    int idxA = CellIndexFromGrid(e.cellA);
                    int idxB = CellIndexFromGrid(e.cellB);

                    if (UFFind(idxA) != UFFind(idxB))
                    {
                        UFUnion(idxA, idxB);

                       
                        grid[e.wall.x, e.wall.y] = 1;

                        
                        e.agentIndex = -1;
                        list[i] = e;

                        edgesRemaining = true;
                    }
                }
            }
        }
    }

    //animated reused from before just with animations can be dropped if not needed
    private IEnumerator GenerateMazeKruskalMultiAgentAnimated()
    {
        List<KruskalEdge> allEdges = BuildKruskalEdges();
        System.Random rng = new System.Random();
        ShuffleList(allEdges, rng);

        
        for (int cx = 0; cx < cellsX; cx++)
        {
            for (int cy = 0; cy < cellsY; cy++)
            {
                Vector2Int cellPos = CellIndexToGrid(cx, cy);
                grid[cellPos.x, cellPos.y] = 1;
                SpawnFloorAt(cellPos);
            }
        }

        int agents = Mathf.Max(1, agentCount);
        List<KruskalEdge>[] edgesPerAgent = new List<KruskalEdge>[agents];
        for (int i = 0; i < agents; i++)
            edgesPerAgent[i] = new List<KruskalEdge>();

        for (int i = 0; i < allEdges.Count; i++)
        {
            KruskalEdge e = allEdges[i];
            int idx = i % agents;
            e.agentIndex = idx;
            edgesPerAgent[idx].Add(e);
        }

        int[] edgeIndices = new int[agents]; 
        for (int i = 0; i < agents; i++) edgeIndices[i] = 0;

        bool carvedSomething = true;

        while (carvedSomething)
        {
            carvedSomething = false;

            for (int agent = 0; agent < agents; agent++)
            {
                List<KruskalEdge> list = edgesPerAgent[agent];

                
                while (edgeIndices[agent] < list.Count)
                {
                    KruskalEdge e = list[edgeIndices[agent]];
                    edgeIndices[agent]++;

                    int idxA = CellIndexFromGrid(e.cellA);
                    int idxB = CellIndexFromGrid(e.cellB);

                    if (UFFind(idxA) != UFFind(idxB))
                    {
                        UFUnion(idxA, idxB);

                        
                        grid[e.wall.x, e.wall.y] = 1;
                        SpawnFloorAt(e.wall);

                        carvedSomething = true;

                        
                        if (generationStepDelay > 0f)
                            yield return new WaitForSeconds(generationStepDelay);
                        else
                            yield return null;

                        break;
                    }
                }
            }
        }
    }

    //build all possible edges for solution
    private List<KruskalEdge> BuildKruskalEdges()
    {
        List<KruskalEdge> edges = new List<KruskalEdge>();

        for (int cx = 0; cx < cellsX; cx++)
        {
            for (int cy = 0; cy < cellsY; cy++)
            {
                Vector2Int a = CellIndexToGrid(cx, cy);

                
                if (cx + 1 < cellsX)
                {
                    Vector2Int b = CellIndexToGrid(cx + 1, cy);
                    Vector2Int wall = new Vector2Int((a.x + b.x) / 2, (a.y + b.y) / 2);
                    edges.Add(new KruskalEdge
                    {
                        cellA = a,
                        cellB = b,
                        wall = wall,
                        agentIndex = 0
                    });
                }

                
                if (cy + 1 < cellsY)
                {
                    Vector2Int b = CellIndexToGrid(cx, cy + 1);
                    Vector2Int wall = new Vector2Int((a.x + b.x) / 2, (a.y + b.y) / 2);
                    edges.Add(new KruskalEdge
                    {
                        cellA = a,
                        cellB = b,
                        wall = wall,
                        agentIndex = 0
                    });
                }
            }
        }

        return edges;
    }

    
    private Vector2Int CellIndexToGrid(int cx, int cy)
    {
        int gx = 1 + cx * 2;
        int gy = 1 + cy * 2;
        return new Vector2Int(gx, gy);
    }

    private int CellIndexFromGrid(Vector2Int gridCell)
    {
        int cx = (gridCell.x - 1) / 2;
        int cy = (gridCell.y - 1) / 2;
        return cy * cellsX + cx;
    }

    #endregion

    #region UNION-FIND HELPERS

    private int UFFind(int x)
    {
        if (ufParent[x] != x)
            ufParent[x] = UFFind(ufParent[x]);
        return ufParent[x];
    }

    private void UFUnion(int a, int b)
    {
        int ra = UFFind(a);
        int rb = UFFind(b);
        if (ra == rb) return;

        if (ufRank[ra] < ufRank[rb])
        {
            ufParent[ra] = rb;
        }
        else if (ufRank[rb] < ufRank[ra])
        {
            ufParent[rb] = ra;
        }
        else
        {
            ufParent[rb] = ra;
            ufRank[ra]++;
        }
    }

    #endregion

    #region VISUAL SPAWNING HELPERS

    private void ClearOldMazeVisual()
    {
        if (mazeParent != null)
        {
            for (int i = mazeParent.childCount - 1; i >= 0; i--)
            {
                Destroy(mazeParent.GetChild(i).gameObject);
            }
        }

        floorTiles.Clear();
        wallTiles.Clear();
        goalInstance = null;
    }

    private void SpawnFloorAt(Vector2Int cell)
    {
        if (floorPrefab == null || mazeParent == null) return;

        if (floorTiles.ContainsKey(cell))
            return;

        Vector3 pos = CellToWorld(cell);
        GameObject tile = Instantiate(floorPrefab, pos, Quaternion.identity, mazeParent);
        floorTiles[cell] = tile;
    }

    private void SpawnWallAt(Vector2Int cell)
    {
        if (wallPrefab == null || mazeParent == null) return;

        Vector3 pos = CellToWorld(cell) + Vector3.up; // raise wall a bit
        GameObject tile = Instantiate(wallPrefab, pos, Quaternion.identity, mazeParent);
        wallTiles.Add(tile);
    }

    private void SpawnGoalAt(Vector2Int cell)
    {
        if (goalPrefab == null || mazeParent == null) return;

        Vector3 pos = CellToWorld(cell) + Vector3.up * 0.5f;
        goalInstance = Instantiate(goalPrefab, pos, Quaternion.identity, mazeParent);
    }

    
    private void BuildFullMazeVisual()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);

                if (grid[x, y] == 1)
                {
                    SpawnFloorAt(cell);
                }
                else
                {
                    SpawnWallAt(cell);
                }
            }
        }

        SpawnGoalAt(goalCell);
    }

    
    private void BuildRemainingWallsAndGoalOnly()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (grid[x, y] == 0)
                {
                    SpawnWallAt(cell);
                }
            }
        }

        SpawnGoalAt(goalCell);
    }

    #endregion

    #region DIFFICULTY (EXTRA LOOPS)

    // difficulty
    
    private void AddExtraConnectionsBasedOnDifficulty(bool spawnVisuals)
    {
        if (difficulty >= 0.999f) return; 

        List<Vector2Int> candidates = new List<Vector2Int>();
        Vector2Int[] dirs4 =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (grid[x, y] != 0) continue; 

                int floorNeighbors = 0;
                foreach (var d in dirs4)
                {
                    int nx = x + d.x;
                    int ny = y + d.y;
                    if (grid[nx, ny] == 1) floorNeighbors++;
                }

                if (floorNeighbors >= 2)
                {
                    candidates.Add(new Vector2Int(x, y));
                }
            }
        }

        if (candidates.Count == 0) return;

        System.Random rng = new System.Random();
        ShuffleList(candidates, rng);

        int toCarve = Mathf.RoundToInt(candidates.Count * (1f - difficulty));
        toCarve = Mathf.Clamp(toCarve, 0, candidates.Count);

        for (int i = 0; i < toCarve; i++)
        {
            Vector2Int c = candidates[i];
            grid[c.x, c.y] = 1;

            if (spawnVisuals)
            {
                SpawnFloorAt(c);
            }
        }
    }

    #endregion

    #region SOLVABILITY CHECK (DEBUG)

    
    private bool HasPath(Vector2Int start, Vector2Int goal)
    {
        if (!IsWalkable(start) || !IsWalkable(goal)) return false;

        bool[,] visited = new bool[width, height];
        Queue<Vector2Int> q = new Queue<Vector2Int>();
        q.Enqueue(start);
        visited[start.x, start.y] = true;

        Vector2Int[] dirs =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        while (q.Count > 0)
        {
            Vector2Int c = q.Dequeue();
            if (c == goal) return true;

            foreach (Vector2Int d in dirs)
            {
                Vector2Int n = c + d;
                if (n.x < 0 || n.x >= width || n.y < 0 || n.y >= height)
                    continue;
                if (visited[n.x, n.y]) continue;
                if (grid[n.x, n.y] != 1) continue;

                visited[n.x, n.y] = true;
                q.Enqueue(n);
            }
        }

        return false;
    }

    #endregion

    #region UTILS

    private static void ShuffleList<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int k = rng.Next(0, i + 1);
            (list[i], list[k]) = (list[k], list[i]);
        }
    }

    #endregion
}
