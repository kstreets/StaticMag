using UnityEngine;

public struct LadderData {
    public RaycastHit hit;
    public Vector3 highestPos;
}

public static class Raycast {
    
    public static bool Ladder(Vector3 start, Vector3 end, Vector3 rayDir, out LadderData data, int rayCount, LayerMask mask) {
        data = default;
        bool success = false;
        
        for (int i = 0; i < rayCount; i++) {
            float comp = (float)i / (rayCount - 1);
            Vector3 curRayPos = Vector3.Lerp(start, end, comp);
            
            if (Physics.Raycast(curRayPos, rayDir, out RaycastHit hit, 1f, mask) && HitWall(hit)) {
                data = new() { hit = hit, highestPos = curRayPos };
                success = true;
                continue;
            }
            
            break;
        }
        
        return success;
    }

    public static bool WallCast(Vector3 origin, Vector3 dir, out RaycastHit hitInfo, float maxDist, int layerMask) {
        return Physics.Raycast(origin, dir, out hitInfo, maxDist, layerMask) && HitWall(hitInfo);
    }
    
    public static bool HitWall(RaycastHit hit) {
        const float angleLeeway = 2f;
        float angleToWall = Vector3.Angle(Vector3.up, hit.normal);
        return (angleToWall <= 90f + angleLeeway && angleToWall >= 90f - angleLeeway);
    }
    
}