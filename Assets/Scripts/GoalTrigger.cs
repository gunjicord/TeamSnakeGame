using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoalTrigger : MonoBehaviour
{
    private bool gameEnded = false;

    private void Reset()
    {
        
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (gameEnded) return;

        Debug.Log($"[GoalTrigger] Trigger enter with: {other.name}, tag={other.tag}");

        
        if (!other.CompareTag("Player") && !other.CompareTag("AISnake"))
            return;

        gameEnded = true;

        
        EndGame(other.name);
    }

    private void EndGame(string who)
    {
        Debug.Log($"GAME OVER — {who} reached the goal!");

        
        Time.timeScale = 0f;

        
    }
}
