using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

public class URPMath
{

    public static Matrix4x4 makeViewMatrix4x4(Camera camera)
    {

        Quaternion cameraRotation = camera.transform.rotation;
        //Vector3 cameraDir = RotateXYZ(Vector3.forward, camera.transform.rotation.eulerAngles);
        Vector3 cameraDir = camera.transform.rotation * Vector3.forward;

        Vector3 front = cameraDir.normalized;
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, front).normalized;
        up = Vector3.Cross(front, right).normalized;
        front = -front;
        Vector3 position;
        Vector3 cameraPos = -camera.transform.position;

        position.x = Vector3.Dot(cameraPos, right);
        position.y = Vector3.Dot(cameraPos, up);
        position.z = Vector3.Dot(cameraPos, front);

        //这里考虑左手坐标系，即z轴朝后
        Matrix4x4 worldToView = new Matrix4x4(new Vector4(right.x, up.x, front.x, 0.0f),
                                      new Vector4(right.y, up.y, front.y, 0.0f),
                                      new Vector4(right.z, up.z, front.z, 0.0f),
                                      new Vector4(position.x, position.y, position.z, 1.0f));
        return worldToView;
    }

    public static Matrix4x4 makeLightViewMatrix4x4(Light light, Vector3 lightWorldPos)
    {

        Quaternion cameraRotation = light.transform.rotation;
        Vector3 cameraDir = light.transform.rotation * Vector3.forward;

        Vector3 front = cameraDir.normalized;
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.Cross(up, front).normalized;
        up = Vector3.Cross(front, right).normalized;
        front = -front;
        Vector3 position;
        Vector3 cameraPos = -lightWorldPos;

        position.x = Vector3.Dot(cameraPos, right);
        position.y = Vector3.Dot(cameraPos, up);
        position.z = Vector3.Dot(cameraPos, front);

        //这里考虑左手坐标系，即z轴朝后
        Matrix4x4 worldToView = new Matrix4x4(new Vector4(right.x, up.x, front.x, 0.0f),
                                      new Vector4(right.y, up.y, front.y, 0.0f),
                                      new Vector4(right.z, up.z, front.z, 0.0f),
                                      new Vector4(position.x, position.y, position.z, 1.0f));
        return worldToView;
    }

    public static Matrix4x4 makeProjectionMatrix4x4(Camera camera)
    {

        float fov = camera.fieldOfView;
        float near = camera.nearClipPlane;
        float far = camera.farClipPlane;
        float top = Mathf.Tan(fov / 360.0f * MathF.PI) * near;
        float bottom = -top;
        float right = top * camera.aspect;
        float left = -right;

        //DrixectX版本，即z属于-1-1
        /*
        Matrix4x4 projectionMatrix = new Matrix4x4(new Vector4((2.0f * near) / (right - left), 0.0f, 0.0f, (right + left)/(right - left)), 
                                                   new Vector4(0.0f, (2.0f * near) / (top - bottom), 0.0f, (top + bottom) / (top - bottom)), 
                                                   new Vector4(0.0f, 0.0f, far / (far - near), -(near * far) / (far - near)), 
                                                   new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        */
        Matrix4x4 projectionMatrix = new Matrix4x4(new Vector4(near / right, 0.0f, 0.0f, 0.0f),
                                                   new Vector4(0.0f, near / top, 0.0f, 0.0f),
                                                   new Vector4(0.0f, 0.0f, -(far + near) / (far - near), -1.0f),
                                                   //new Vector4((right + left) / (right - left), (top + bottom) / (top - bottom), -(near * far) / (far - near), 0.0f));
                                                   new Vector4(0.0f , 0.0f, -2.0f * (near * far) / (far - near), 0.0f));
        return projectionMatrix;
    }

    public static Matrix4x4 makeLightProjectionMatrix4x4(Matrix4x4 projectionMatrix)
    {



        return new Matrix4x4();
    }

    public static Vector4 mul(Matrix4x4 matrix, Vector4 dir)
    {
        Vector4 rotateDir;
        rotateDir.x = Vector4.Dot(matrix.GetRow(0), dir);
        rotateDir.y = Vector4.Dot(matrix.GetRow(1), dir);
        rotateDir.z = Vector4.Dot(matrix.GetRow(2), dir);
        rotateDir.w = Vector4.Dot(matrix.GetRow(3), dir);
        return rotateDir;
    }
    public static Vector3 RotateXYZ(Vector3 dir, Vector3 xyz)
    {
        float x = xyz.x / 180.0f * Mathf.PI;
        float y = xyz.y / 180.0f * Mathf.PI;
        float z = xyz.z / 180.0f * Mathf.PI;
        Vector4 dirRotate = new Vector4(dir.x, dir.y, dir.z, 0.0f);

        Matrix4x4 rotateX = new Matrix4x4(new Vector4(1.0f, 0.0f, 0.0f, 0.0f),
                                          new Vector4(0.0f, Mathf.Cos(x), Mathf.Sin(x), 0.0f),
                                          new Vector4(0.0f, -Mathf.Sin(x), Mathf.Cos(x), 0.0f),
                                          new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        dirRotate = mul(rotateX, dirRotate);

        Matrix4x4 rotateY = new Matrix4x4(new Vector4(Mathf.Cos(y), 0.0f, -Mathf.Sin(y), 0.0f),
                                          new Vector4(0.0f, 1.0f, 0.0f, 0.0f),
                                          new Vector4(Mathf.Sin(y), 0.0f, Mathf.Cos(y), 0.0f),
                                          new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

        dirRotate = mul(rotateY, dirRotate);
        Matrix4x4 rotateZ = new Matrix4x4(new Vector4(Mathf.Cos(z), Mathf.Sin(z), 0.0f, 0.0f),
                                  new Vector4(-Mathf.Sin(z), Mathf.Cos(z), 0.0f, 0.0f),
                                  new Vector4(0.0f, 0.0f, 1.0f, 0.0f),
                                  new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        dirRotate = mul(rotateZ, dirRotate);

        return dirRotate;

    }

    public static Vector4 transVec3ToVec4(Vector3 a)
    {
        return new Vector4(a.x, a.y, a.z, 1.0f);
    }
    public static Vector3 vec3Mulvec3(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
    }
    public static Vector3 vec3Divvec3(Vector3 a, Vector3 b)
    {
        return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }

    public static Vector3Int floatVec3TOIntVec3(Vector3 a, int mode, int k)
    {
        if(k == 0)
        {
            return Vector3Int.zero;
        }
        if(mode == 0)
        {
            return new Vector3Int(Mathf.FloorToInt(a.x / k), Mathf.FloorToInt(a.y / k), Mathf.FloorToInt(a.z / k)) * k;
        }
        else
        {
            return new Vector3Int(Mathf.CeilToInt(a.x / k), Mathf.CeilToInt(a.y / k), Mathf.CeilToInt(a.z / k)) * k;
        }

    }

    public static Vector3 floatVec3ToKMul(Vector3 a, int mode, float k)
    {
        if (k == 0.0f)
        {
            return Vector3.zero;
        }
        if (mode == 0)
        {
            return new Vector3(Mathf.Floor(a.x / k), Mathf.Floor(a.y / k), Mathf.Floor(a.z / k)) * k;
        }
        else
        {
            return new Vector3(Mathf.Ceil(a.x / k), Mathf.Ceil(a.y / k), Mathf.Ceil(a.z / k)) * k;
        }
    }

    public static Vector3 Vec3Opfloat(Vector3 a, float b, int k)
    {
        if(k == 0)//减法
        {
            return new Vector3(a.x - b, a.y - b, a.z - b);
        }
        else//加法
        {
            return new Vector3(a.x + b, a.y + b, a.z + b);
        }
    }

    public static Vector3 getDirFromAngle(float theta, float phi)
    {
        return new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta));
    }

}
