using System.Collections.Generic;
using UnityEngine;

//simple snake with memory
//greedy failed but the class is still named this. might change it later but for now its a good reminder for the final talking points. 
//greedy failed because it couldnt remember and would get stuck going back and forth. Same thing that was used for the sudoky solver with DFS and backtracking.
public class AISnakeGreedyController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float stepDelay = 0.3f;

    private Vector2Int currentCell;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private float stepTimer = 0f;

    private readonly Vector3 heightOffset = new Vector3(0, 0.5f, 0);

    private bool initialized = false;
    private bool reachedGoal = false;

    private HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
    private Stack<Vector2Int> backtrackStack = new Stack<Vector2Int>();

    private void Start()
    {
        StartCoroutine(WaitForMaze());
    }

    private System.Collections.IEnumerator WaitForMaze()
    {
        while (MazeGenerator.Instance == null)
            yield return null;

        while (MazeGenerator.Instance.Grid == null || MazeGenerator.Instance.IsGenerating)
            yield return null;

        currentCell = MazeGenerator.Instance.startCell;
        targetPosition = MazeGenerator.Instance.CellToWorld(currentCell) + heightOffset;
        transform.position = targetPosition;

        visited.Clear();
        backtrackStack.Clear();
        visited.Add(currentCell);

        initialized = true;
        reachedGoal = false;
    }

    private void Update()
    {
        if (!initialized || MazeGenerator.Instance == null || MazeGenerator.Instance.IsGenerating)
            return;

        if (reachedGoal)
            return;

        if (isMoving)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (Vector3.Distance(transform.position, targetPosition) < 0.001f)
            {
                isMoving = false;
            }
            return;
        }

        if (currentCell == MazeGenerator.Instance.goalCell)
        {
            reachedGoal = true;
            return;
        }

        stepTimer += Time.deltaTime;
        if (stepTimer < stepDelay) return;
        stepTimer = 0f;

        Vector2Int goal = MazeGenerator.Instance.goalCell;

        Vector2Int[] dirs =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0)
        };

        List<Vector2Int> unvisitedNeighbors = new List<Vector2Int>();

        foreach (Vector2Int dir in dirs)
        {
            Vector2Int candidate = currentCell + dir;

            if (!MazeGenerator.Instance.IsWalkable(candidate))
                continue;

            if (visited.Contains(candidate))
                continue;

            unvisitedNeighbors.Add(candidate);
        }

        Vector2Int nextCell = currentCell;

        if (unvisitedNeighbors.Count > 0)
        {
            int bestDist = int.MaxValue;
            foreach (var c in unvisitedNeighbors)
            {
                int dist = Mathf.Abs(c.x - goal.x) + Mathf.Abs(c.y - goal.y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nextCell = c;
                }
            }

            backtrackStack.Push(currentCell);
        }
        else
        {
            if (backtrackStack.Count > 0)
            {
                nextCell = backtrackStack.Pop();
            }
            else
            {
                initialized = false;
                return;
            }
        }

        if (nextCell != currentCell)
        {
            currentCell = nextCell;
            visited.Add(currentCell);
            targetPosition = MazeGenerator.Instance.CellToWorld(currentCell) + heightOffset;
            isMoving = true;
        }
    }
}
