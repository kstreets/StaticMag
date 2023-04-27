using UnityEngine;

public static class Vector3Ext {

    public static Vector3 Flatten(Vector3 vector3) {
        return new(vector3.x, 0f, vector3.z);
    }

    public static Vector3 ClosestPointOnLine(Vector3 point, Vector3 start, Vector3 end) {
        Vector3 lineDir = (end - start).normalized;
        Vector3 toPosVector = point - start;
        float dot = Vector3.Dot(toPosVector, lineDir);
        Vector3 closestPoint = start + (lineDir * dot);

        Vector3 centerPos = start + ((end - start) / 2f);
        float dist = Vector3.Distance(centerPos, closestPoint);
        float halfDist = Vector3.Distance(start, centerPos);
        if (dist >= halfDist) {
            Vector3 dir = (closestPoint - centerPos).normalized;
            return centerPos + (dir * halfDist);
        }

        return closestPoint;
    }
    
}