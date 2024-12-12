using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityGif
{
    public class GifPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// 用于对导入的资源进行预处理
        /// </summary>
        /// <param name="importedAssets">所有导入的文件的路径</param>
        /// <param name="deletedAssets">未使用</param>
        /// <param name="movedAssets">未使用</param>
        /// <param name="movedFromAssetPaths">未使用</param>
        public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".gif.bytes"))
                {
                    TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                    if (textAsset != null)
                    {
                        GifData gifData = ScriptableObject.CreateInstance<GifData>();
                        GifDecoder gifDecoder = new GifDecoder(textAsset.bytes);
                        gifData.gifDecoder = gifDecoder;

                        string gifDataAssetPath = Path.ChangeExtension(assetPath, ".asset");
                        AssetDatabase.CreateAsset(gifData, gifDataAssetPath);
                        // 自动为.gif.bytes文件创建一个TextAsset , 删除原始的TextAsset
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                }
            }
        }
    }
}
