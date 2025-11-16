using UnityEngine;

public class CameraMazeFocus : MonoBehaviour
{
    public MazeGenerator mazeGen;

    void Start()
    {
        float posX = ((mazeGen.width - 1) / 2) * mazeGen.cellSize;
        float posY;
        float posZ = ((mazeGen.height - 1) / 2) * mazeGen.cellSize;

        if (posX >= posZ)
        {
            posY = posX * 4;
        }
        else
        {
            posY = posZ * 4;
        }

        this.gameObject.transform.position = new Vector3(posX, posY, posZ);
    }
}
