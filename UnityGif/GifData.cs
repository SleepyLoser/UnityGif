using UnityEngine;

namespace UnityGif
{
    public class GifData : ScriptableObject
    {
        /// <summary>
        /// GIF 的解码器，可获取解码内容
        /// </summary>
        public GifDecoder gifDecoder;
    }
}