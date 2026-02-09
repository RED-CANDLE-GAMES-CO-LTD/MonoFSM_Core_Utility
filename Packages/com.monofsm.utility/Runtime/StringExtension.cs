using UnityEngine;

namespace MonoFSM.Core
{
    public static class StringExtension
    {
        public static string FolderPath(this string path)
        {
            Debug.Log("FolderPath called with path: " + path);
            return path[..path.LastIndexOf('/')];
        }
    }
}
