using System.Collections.Generic;
using UnityEngine;

namespace MonoFSM.Utility
{
    /// <summary>
    /// 生成物件分佈 pattern 的工具類
    /// </summary>
    public static class SpawnPatternUtility
    {
        /// <summary>
        /// 生成圓形分佈的點位
        /// </summary>
        public static List<Vector3> GenerateCirclePattern(int count, float radius, float yOffset = 0f)
        {
            var points = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i * Mathf.Deg2Rad;
                points.Add(new Vector3(
                    Mathf.Cos(angle) * radius,
                    yOffset,
                    Mathf.Sin(angle) * radius
                ));
            }
            return points;
        }

        /// <summary>
        /// 生成網格分佈的點位（從中心展開）
        /// </summary>
        public static List<Vector3> GenerateGridPattern(int columns, int rows, float spacing)
        {
            var points = new List<Vector3>(columns * rows);
            float startX = -(columns - 1) * spacing * 0.5f;
            float startZ = -(rows - 1) * spacing * 0.5f;

            for (int x = 0; x < columns; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    points.Add(new Vector3(
                        startX + x * spacing,
                        0f,
                        startZ + z * spacing
                    ));
                }
            }
            return points;
        }

        /// <summary>
        /// 生成隨機分佈但保證最小距離的點位
        /// </summary>
        public static List<Vector3> GenerateRandomPattern(int count, Vector3 range, float minDistance, int maxAttempts = 30)
        {
            var points = new List<Vector3>(count);

            for (int i = 0; i < count; i++)
            {
                bool found = false;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var candidate = new Vector3(
                        Random.Range(-range.x, range.x),
                        Random.Range(-range.y, range.y),
                        Random.Range(-range.z, range.z)
                    );

                    if (IsValidPosition(candidate, points, minDistance))
                    {
                        points.Add(candidate);
                        found = true;
                        break;
                    }
                }

                // 找不到有效位置，fallback 到隨機
                if (!found)
                {
                    points.Add(new Vector3(
                        Random.Range(-range.x, range.x),
                        Random.Range(-range.y, range.y),
                        Random.Range(-range.z, range.z)
                    ));
                }
            }
            return points;
        }

        private static bool IsValidPosition(Vector3 candidate, List<Vector3> existingPoints, float minDistance)
        {
            float minDistSq = minDistance * minDistance;
            foreach (var point in existingPoints)
            {
                if ((candidate - point).sqrMagnitude < minDistSq)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Fisher-Yates Shuffle（原地洗牌）
        /// </summary>
        public static void Shuffle<T>(IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        /// <summary>
        /// 取得 shuffled 後的點位（不修改原 list）
        /// </summary>
        public static List<T> GetShuffled<T>(IList<T> source)
        {
            var result = new List<T>(source);
            Shuffle(result);
            return result;
        }
    }
}
