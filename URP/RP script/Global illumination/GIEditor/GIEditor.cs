using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using static GISetting;
using System;

[ExecuteInEditMode]
public class GIEditor : MonoBehaviour
{

    public enum GIEditorMode
    {
        CustomMode,
        SystemMode
    }

    public GIEditorMode giEditorMode;
    public bool SSLPV;
    public Vector3 VoxelBoxCenterPoint;
    private Vector3 VoxelBoxStartPoint;
    //VoxelBoxSizeBase只会被直接修改值，而不因缩放而改变
    private Vector3Int VoxelBoxSizeBase;
    public Vector3Int VoxelBoxSize;

    //体素为立方体
    public float VoxelSize;
    public float Intensity;

    public float[] voxelBox;
    Vector3 voxelBoxSizeScale;

    public GIEditor(float[] voxelBox)
    {
        VoxelBoxSize = Vector3Int.one;
        this.voxelBox = voxelBox;
        this.voxelBoxSizeScale = Vector3.one;
    }

    private void OnValidate()
    {
        if (this.voxelBoxSizeScale == this.transform.localScale && VoxelBoxSizeBase != VoxelBoxSize)
        {
            VoxelBoxSizeBase = VoxelBoxSize;
        }
        this.voxelBoxSizeScale = this.transform.localScale;

    }

    public void setUp()
    {
        if (this.giEditorMode == GIEditorMode.SystemMode)
        {
            /*
            this.VoxelBoxCenterPoint = new Vector3(voxelBox[0], voxelBox[1], voxelBox[2]);
            Vector3 VoxelBoxLength = new Vector3(voxelBox[3], voxelBox[4], voxelBox[5]) * 2;

            Vector3 voxelBoxSizeTemp = VoxelBoxLength / VoxelSize;
            this.VoxelBoxSize = URPMath.floatVec3TOIntVec3(voxelBoxSizeTemp, 1, 1);
            */
        }
        else
        {
            this.VoxelBoxCenterPoint = this.transform.position;
            this.VoxelBoxSize = URPMath.floatVec3TOIntVec3(URPMath.vec3Mulvec3((Vector3)(VoxelBoxSizeBase), this.transform.localScale), 1, 1);
        }

        Vector3 halfVoxelOffset = new Vector3(VoxelBoxSize.x % 2, VoxelBoxSize.y % 2, VoxelBoxSize.z % 2);
        VoxelBoxStartPoint = VoxelBoxCenterPoint - (Vector3)VoxelBoxSize * VoxelSize / 2 - halfVoxelOffset * 0.5f;

    }

    public void OnDrawGizmos()
    {

        setUp();

        for (int x = 0; x < VoxelBoxSize.x; x++)
        {
            for (int y = 0; y < VoxelBoxSize.y; y++)
            {
                for (int z = 0; z < VoxelBoxSize.z; z++)
                {
                    Vector3 voxelPos = VoxelBoxStartPoint + new Vector3(x * VoxelSize, y * VoxelSize, z * VoxelSize);
                    voxelPos += new Vector3(VoxelSize, VoxelSize, VoxelSize) * 0.5f;
                    //Gizmos.DrawSphere(voxelPos, 0.05f);
                    Gizmos.color = Color.yellow;
                    if (SSLPV)
                    {
                        //Gizmos.DrawWireSphere(voxelPos, VoxelSize / 2);
                        Gizmos.DrawWireCube(voxelPos, new Vector3(VoxelSize, VoxelSize, VoxelSize));
                    }
                    else
                    {
                        Gizmos.DrawWireCube(voxelPos, new Vector3(VoxelSize, VoxelSize, VoxelSize));
                    }
                }
            }
        }

    }

}
