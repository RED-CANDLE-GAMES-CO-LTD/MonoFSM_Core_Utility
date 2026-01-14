#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class EditorMonoNodeExtension
{
    [Conditional("UNITY_EDITOR")]
    public static void LogErrorUsedCheck(this MonoBehaviour target, string message)
    {
        Debug.LogError("有是缺東西沒綁到還是要關掉/刪掉？" + message, target);
    }

    public static void CopyToClipboard(this string str)
    {
        GUIUtility.systemCopyBuffer = str;
    }

    public static IEnumerable<Type> FindSubClassesOf(this MonoBehaviour owner, Type type)
    {
        var baseType = type;
        var assembly = baseType.Assembly;
        return assembly
            .GetTypes()
            .Where(t => t.IsSubclassOf(baseType) || (t == type && t.IsAbstract == false));
    }

    static readonly HashSet<Type> engineAbstracts = new HashSet<Type>
    {
        typeof(Collider),
        typeof(Renderer),
        typeof(Joint),
        typeof(Behaviour),
        // 其他...
    };

    public static bool InheritsFrom<TBase>(this System.Type type)
    {
        return type.InheritsFrom(typeof(TBase));
    }

    public static bool InheritsFrom(this Type type, Type baseType)
    {
        if (baseType.IsAssignableFrom(type))
            return true;
        if (type.IsInterface && !baseType.IsInterface)
            return false;
        if (baseType.IsInterface)
            return type.GetInterfaces().Contains(baseType);
        for (Type type1 = type; type1 != null; type1 = type1.BaseType)
        {
            if (
                type1 == baseType
                || baseType.IsGenericTypeDefinition
                    && type1.IsGenericType
                    && type1.GetGenericTypeDefinition() == baseType
            )
                return true;
        }
        return false;
    }

    /// <summary>
    /// Find all class types that derives from the given <see cref="baseType"/> and:
    /// 1. aren't generic or abstract if the <see cref="baseType"/> is a class.
    /// 2. are <see cref="MonoBehaviour"/>s if the <see cref="baseType"/> is an interface.
    /// If the <see cref="baseType"/> is an interface, the list will only include inherited types that are <see cref="MonoBehaviour"/>.
    /// </summary>
    /// <param name="baseType"></param>
    /// <returns></returns>
    public static List<Type> FilterSubClassOrImplementationFromDomain(this Type baseType)
    {
        var typeList = new List<Type>();

        //單獨判斷 baseType ?
        if (baseType.IsClass && !baseType.IsAbstract && !baseType.IsGenericType)
        {
            if (!engineAbstracts.Contains(baseType)) //不加入Unity引擎的抽象類型
            {
                typeList.Add(baseType);
            }
            // We also want to include the given type if it's not an abstract or a generic type.

            Debug.Log("Add type " + baseType.Name + " from itself");
        }

        var types = TypeCache.GetTypesDerivedFrom(baseType);

        //interface型
        if (baseType.IsInterface)
        {
            foreach (var type in types)
            {
                if (type.InheritsFrom<MonoBehaviour>() && !type.IsAbstract && !type.IsGenericType)
                {
                    // 只要是實作了 baseType 的 MonoBehaviour 類型
                    typeList.Add(type);
                }
            }
        }
        else // if baseType.IsClass
        {
            foreach (var type in types)
            {
                if (type.IsClass && !type.IsAbstract && !type.IsGenericType)
                {
                    typeList.Add(type);
                    Debug.Log("Add type " + type.Name + " from " + baseType.Name);
                }
            }
        }
        return typeList;
    }

    /// <summary>
    /// Find all subclasses of the <see cref="baseType"/> who have the given attribute.
    /// </summary>
    /// <param name="baseType"></param>
    /// <param name="attribute"></param>
    /// <returns></returns>
    public static List<Type> FilterSubClassFromDomain(this Type baseType, Type attribute)
    {
        var typeList = new List<Type>();
        var types = TypeCache.GetTypesDerivedFrom(baseType);
        foreach (Type type in types)
        {
            if (
                type.IsClass
                && !type.IsAbstract
                && !type.IsGenericType
                && type.GetCustomAttributes(attribute, true).Length > 0
            )
            {
                typeList.Add(type);
            }
        }

        return typeList;
    }

    public static IEnumerable<Type> GetAllScriptableAssetType()
    {
        //[]: 好像不一定需要這個attribute 才能拿? 但這個是介面問題，不該拿到不能變成asset的SO，但不確定有需要動態產生SO嗎？
        var types = typeof(ScriptableObject).FilterSubClassFromDomain(
            typeof(CreateAssetMenuAttribute)
        );
        return types;
    }
}
#endif
