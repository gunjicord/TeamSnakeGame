using UnityEngine;

//simple snake for now
public class AISnakeGreedyController : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float stepDelay = 0.3f;

    private Vector2Int currentCell;
    private Vector2Int lastCell;
    private Vector3 targetPosition;
    private bool isMoving = false;
    private float stepTimer = 0f;

    private readonly Vector3 heightOffset = new Vector3(0, 0.5f, 0);

    private bool initialized = false;

    private void Start()
    {
        
        //wait for maze to finish gen
        StartCoroutine(WaitForMaze());
    }

    private System.Collections.IEnumerator WaitForMaze()
    {
        //wait until maze isgenerated
        while (MazeGenerator.Instance.IsGenerating)
            yield return null;

        //force snake to spawn at maze start
        currentCell = MazeGenerator.Instance.startCell;


        //snap to the correct position
        targetPosition = MazeGenerator.Instance.CellToWorld(currentCell) + heightOffset;
        transform.position = targetPosition;

        lastCell = new Vector2Int(-999, -999);
        initialized = true;
    }


    private void Update()
    {
        
        if (!initialized || MazeGenerator.Instance.IsGenerating)
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

        //delay between
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

        Vector2Int bestCell = currentCell;
        int bestDist = int.MaxValue;
        bool foundBetter = false;

        foreach (Vector2Int dir in dirs)
        {
            Vector2Int candidate = currentCell + dir;

            //skip walls no check
            if (!MazeGenerator.Instance.IsWalkable(candidate))
                continue;

            //avoid back into previous cell
            if (candidate == lastCell)
                continue;

            //distance to goal
            int dist = Mathf.Abs(candidate.x - goal.x) + Mathf.Abs(candidate.y - goal.y);

            if (dist < bestDist)
            {
                bestDist = dist;
                bestCell = candidate;
                foundBetter = true;
            }
        }

        //if nothing found go back
        if (!foundBetter)
        {
            foreach (Vector2Int dir in dirs)
            {
                Vector2Int candidate = currentCell + dir;

                if (MazeGenerator.Instance.IsWalkable(candidate))
                {
                    bestCell = candidate;
                    break;
                }
            }
        }

        //move if possible
        if (bestCell != currentCell)
        {
            lastCell = currentCell;
            currentCell = bestCell;
            targetPosition = MazeGenerator.Instance.CellToWorld(currentCell) + heightOffset;
            isMoving = true;
        }
    }
}
