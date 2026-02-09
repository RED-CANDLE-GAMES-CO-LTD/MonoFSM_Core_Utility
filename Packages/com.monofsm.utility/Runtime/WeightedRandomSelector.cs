using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonoFSM.Utility
{
    /// <summary>
    /// 通用加權隨機選取工具
    /// </summary>
    public static class WeightedRandomSelector
    {
        /// <summary>
        /// 依權重選取單一項目（累積權重法）
        /// </summary>
        public static T SelectOne<T>(IList<T> items, Func<T, float> weightSelector)
        {
            if (items == null || items.Count == 0)
            {
                Debug.LogError("WeightedRandomSelector: items is null or empty");
                return default;
            }

            float totalWeight = 0f;
            foreach (var item in items)
                totalWeight += weightSelector(item);

            if (totalWeight <= 0f)
            {
                Debug.LogWarning("WeightedRandomSelector: total weight is 0, returning first item");
                return items[0];
            }

            float random = UnityEngine.Random.Range(0f, totalWeight);
            float cumulative = 0f;

            foreach (var item in items)
            {
                cumulative += weightSelector(item);
                if (random <= cumulative)
                    return item;
            }

            return items[items.Count - 1];
        }

        /// <summary>
        /// 依權重選取 N 個項目
        /// </summary>
        public static List<T> SelectN<T>(IList<T> items, int count, Func<T, float> weightSelector, bool allowDuplicates = false)
        {
            if (items == null || items.Count == 0)
            {
                Debug.LogError("WeightedRandomSelector: items is null or empty");
                return new List<T>();
            }

            var result = new List<T>(count);

            if (allowDuplicates)
            {
                for (int i = 0; i < count; i++)
                {
                    var selected = SelectOne(items, weightSelector);
                    result.Add(selected);
                }
            }
            else
            {
                var availableItems = new List<T>(items);
                var availableWeights = new Dictionary<T, float>();
                foreach (var item in items)
                    availableWeights[item] = weightSelector(item);

                int selectCount = Mathf.Min(count, availableItems.Count);
                for (int i = 0; i < selectCount; i++)
                {
                    var selected = SelectOne(availableItems, item => availableWeights[item]);
                    result.Add(selected);
                    availableItems.Remove(selected);
                }
            }

            return result;
        }

        /// <summary>
        /// 依獨立機率篩選（每個項目獨立擲骰）
        /// </summary>
        public static List<T> SelectByProbability<T>(IList<T> items, Func<T, float> probabilitySelector)
        {
            if (items == null || items.Count == 0)
                return new List<T>();

            var result = new List<T>();
            foreach (var item in items)
            {
                float probability = probabilitySelector(item);
                if (UnityEngine.Random.value <= probability)
                    result.Add(item);
            }

            return result;
        }
    }
}
