﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityGif
{
    public class GifImage : MonoBehaviour
    {
        /// <summary>
        /// GifImage 单例
        /// </summary>
        private static GifImage instance;

        /// <summary>
        /// GifImage 单例只读属性
        /// </summary>
        public static GifImage Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GifImage>();
                    if (instance == null)
                    {
                        GameObject gameObject = new GameObject("GifImage");
                        instance = gameObject.AddComponent<GifImage>();
                        DontDestroyOnLoad(gameObject);
                    }
                } 
                return instance; 
            }
        }

        /// <summary>
        /// 纹理过滤模式（全局）
        /// </summary>
        private FilterMode filterMode = FilterMode.Bilinear;
        public FilterMode FilterMode
        {
            get{ return filterMode; }
            set{ filterMode = value; }
        }

        /// <summary>
        /// 纹理包裹模式（全局）
        /// </summary>
        private TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        public TextureWrapMode WrapMode
        {
            get{ return wrapMode; }
            set{ wrapMode = value; }
        }

        /// <summary>
        /// 用于控制暂停 GIF 的锁
        /// </summary>
        private static bool lockForPause = false;

        /// <summary>
        /// 用于控制停止 GIF 的锁
        /// </summary>
        private static readonly Object lockForStop = new Object();

        /// <summary>
        /// 用于控制清除 GIF 的锁
        /// </summary>
        private static readonly Object lockForClear = new Object();

        /// <summary>
        /// GIF 纹理列表仓库
        /// </summary>
        private Dictionary<string, List<GifTexture>> gifTextureWarehouse = new Dictionary<string, List<GifTexture>>();

        /// <summary>
        /// GIF 纹理初始化监测
        /// </summary>
        private Dictionary<string, bool> gifInitialization = new Dictionary<string, bool>();
        
        /// <summary>
        /// GIF Play() 协程监测
        /// </summary>
        private List<Coroutine> gifPlayCoroutine = new List<Coroutine>();

        /// <summary>
        /// 用于播放 GIF 的 RawImage 哈希值对照表
        /// </summary>
        private Dictionary<int, RawImage> gifRawImage = new Dictionary<int, RawImage>();

        /// <summary>
        /// GIF 状态
        /// </summary>
        public Dictionary<string, Dictionary<int, State>> gifState = new Dictionary<string, Dictionary<int, State>>();

        /// <summary>
        /// GIF 播放协程
        /// </summary>
        private Dictionary<string, Dictionary<int, Coroutine>> gifCoroutine = new Dictionary<string, Dictionary<int, Coroutine>>();

        /// <summary>
        /// GIF 状态
        /// </summary>
        public enum State
        {
            /// <summary>
            /// GIF 未初始化
            /// </summary>
            None,

            /// <summary>
            /// GIF 正在加载纹理
            /// </summary>
            Loading,

            /// <summary>
            /// GIF 纹理已加载完毕
            /// </summary>
            Ready,

            /// <summary>
            /// GIF 正在播放
            /// </summary>
            Playing,

            /// <summary>
            /// GIF 已暂停
            /// </summary>
            Pause
        }



        /// <summary>
        /// 播放 GIF
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="rawImage">指定 GIF 在哪个 RawImage 上播放</param>
        public void Play(GifData gifData, RawImage rawImage)
        {
            if (gifData == null || rawImage == null)
            {
                #if UNITY_EDITOR
                Debug.LogError("GIF 数据或 RawImage 为空！！！");
                #endif
                return;
            }
            lock (lockForClear)
            {
                Coroutine coroutine = StartCoroutine(PlayInternal(gifData, rawImage));
                gifPlayCoroutine.Add(coroutine);
            }
        }

        /// <summary>
        /// 暂停所有 GIF 的播放
        /// </summary>
        public void Pause()
        {
            if (!lockForPause)
            {
                lockForPause = true;
                Dictionary<string, Dictionary<int, Coroutine>>.Enumerator enumerator = gifCoroutine.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Dictionary<int, Coroutine>.Enumerator e = gifCoroutine[enumerator.Current.Key].GetEnumerator();
                    while (e.MoveNext())
                    {
                        gifState[enumerator.Current.Key][e.Current.Key] = State.Pause;
                    }
                }
                lockForPause = false;
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogWarning("GIF 正在暂停中，无需重复暂停");
                #endif
            }
        }

        /// <summary>
        /// 暂停单个 GIF 的播放
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="rawImage">指定 GIF 在哪个 RawImage 上播放</param>
        public void Pause(GifData gifData, RawImage rawImage)
        {
            if (gifData == null || rawImage == null)
            {
                #if UNITY_EDITOR
                Debug.LogError("GIF 数据或 rawImage 二者其一为空");
                #endif
                return;
            }

            int rawImageHashCode = rawImage.GetHashCode();
            if (gifCoroutine.ContainsKey(gifData.name) && gifCoroutine[gifData.name].ContainsKey(rawImageHashCode))
            {
                gifState[gifData.name][rawImageHashCode] = State.Pause;
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogWarning("状态列表中不存在相关键");
                #endif
            }
        }

        /// <summary>
        /// 停止所有 GIF 的播放
        /// </summary>
        public void Stop()
        {
            lock (lockForStop)
            {
                if (gifCoroutine.Count == 0) return;
                Dictionary<string, Dictionary<int, Coroutine>>.Enumerator enumerator = gifCoroutine.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Dictionary<int, Coroutine>.Enumerator e = gifCoroutine[enumerator.Current.Key].GetEnumerator();
                    while (e.MoveNext())
                    {
                        gifState[enumerator.Current.Key][e.Current.Key] = State.Ready;
                        StopCoroutine(gifCoroutine[enumerator.Current.Key][e.Current.Key]);
                        gifRawImage[e.Current.Key].texture = null;
                        gifRawImage.Remove(e.Current.Key);
                    }
                }
                gifCoroutine.Clear();
            }
        }

        /// <summary>
        /// 停止单个 GIF 的播放
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="rawImage">指定 GIF 在哪个 RawImage 上播放</param>
        public void Stop(GifData gifData, RawImage rawImage)
        {
            if (gifData == null || rawImage == null)
            {
                #if UNITY_EDITOR
                Debug.LogError("GIF 数据或 rawImage 二者其一为空");
                #endif
                return;
            }

            int rawImageHashCode = rawImage.GetHashCode();
            if (gifCoroutine.ContainsKey(gifData.name) && gifCoroutine[gifData.name].ContainsKey(rawImageHashCode))
            {
                gifState[gifData.name][rawImageHashCode] = State.Ready;
                StopCoroutine(gifCoroutine[gifData.name][rawImageHashCode]);
                rawImage.texture = null;
                gifCoroutine[gifData.name].Remove(rawImageHashCode);
                gifRawImage.Remove(rawImageHashCode);
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogWarning("状态列表中不存在相关键");
                #endif
            }
        }

        /// <summary>
        ///  清除所有 GIF 的纹理数据（默认不使用对象池处理纹理）
        /// </summary>
        /// <param name="pool">是否使用对象池处理纹理</param>
        public void Clear(bool pool = false)
        {
            StartCoroutine(ClearInternal(pool));
        }

        /// <summary>
        /// 清除单个 GIF 的纹理数据（默认不使用对象池处理纹理）
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="pool">是否使用对象池处理纹理</param>
        public void Clear(GifData gifData, bool pool = false)
        {
            StartCoroutine(ClearInternal(gifData, pool));
        }

        /// <summary>
        /// 清除所有 GIF 的纹理数据（默认不使用对象池处理纹理）
        /// </summary>
        /// <param name="pool">是否使用对象池处理纹理</param>
        /// <returns>迭代器</returns>
        private IEnumerator ClearInternal(bool pool)
        {
            lock (lockForClear)
            {
                if (gifTextureWarehouse.Count == 0) yield break;

                for (int i = 0; i < gifPlayCoroutine.Count; ++i)
                {
                    yield return gifPlayCoroutine[i];
                }
                gifPlayCoroutine.Clear();

                Dictionary<string, Dictionary<int, Coroutine>>.Enumerator enumerator = gifCoroutine.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    Dictionary<int, Coroutine>.Enumerator e = gifCoroutine[enumerator.Current.Key].GetEnumerator();
                    while (e.MoveNext())
                    {
                        gifState[enumerator.Current.Key][e.Current.Key] = State.None;
                        StopCoroutine(gifCoroutine[enumerator.Current.Key][e.Current.Key]);
                        gifRawImage[e.Current.Key].texture = null;
                        gifRawImage.Remove(e.Current.Key);
                    }
                }
                gifCoroutine.Clear();

                if (pool)
                {
                    // 待补充
                }
                else
                {
                    foreach (List<GifTexture> list in gifTextureWarehouse.Values)
                    {
                        for (int i = 0; i < list.Count; ++i)
                        {
                            Destroy(list[i].texture2d);
                        }
                    }
                }
                gifTextureWarehouse.Clear();
            }
        }

        /// <summary>
        /// 清除单个 GIF 的纹理数据（默认不使用对象池处理纹理）
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="pool">是否使用对象池处理纹理</param>
        /// <returns>迭代器</returns>
        private IEnumerator ClearInternal(GifData gifData, bool pool)
        {
            lock (lockForClear)
            {
                if (gifData == null || !gifInitialization.ContainsKey(gifData.name))
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning("GIF 数据为空");
                    #endif
                    yield break;
                }

                for (int i = 0; i < gifPlayCoroutine.Count; ++i)
                {
                    yield return gifPlayCoroutine[i];
                }
                gifPlayCoroutine.Clear();

                if (gifInitialization[gifData.name])
                {
                    gifInitialization[gifData.name] = false;
                    foreach (KeyValuePair<int, Coroutine> kv in gifCoroutine[gifData.name])
                    {
                        gifState[gifData.name][kv.Key] = State.None;
                        StopCoroutine(kv.Value);
                        gifRawImage[kv.Key].texture = null;
                        gifRawImage.Remove(kv.Key);
                    }
                    gifCoroutine.Remove(gifData.name);

                    if (pool)
                    {
                        // 待补充
                    }
                    else
                    {
                        for (int i = 0; i < gifTextureWarehouse[gifData.name].Count; ++i)
                        {
                            Destroy(gifTextureWarehouse[gifData.name][i].texture2d);
                        }
                    }
                    gifTextureWarehouse.Remove(gifData.name);
                }
                else
                {
                    #if UNITY_EDITOR
                    Debug.LogWarning("GIF 数据正在清除或已被清理");
                    #endif
                }   
            }
        }

        /// <summary>
        /// 播放 GIF
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="rawImage">指定 GIF 在哪个 RawImage 上播放</param>
        /// <returns>迭代器</returns>
        private IEnumerator PlayInternal(GifData gifData, RawImage rawImage)
        {
            int rawImageHashCode = rawImage.GetHashCode();

            // 预防重复启动同一 GIF
            if (gifState.ContainsKey(gifData.name) && gifState[gifData.name].ContainsKey(rawImageHashCode))
            {
                if (gifState[gifData.name][rawImageHashCode] == State.Loading)
                {
                    #if UNITY_EDITOR
                    Debug.LogWarningFormat("{0} 正在加载纹理！！！", gifData.name);
                    #endif
                    yield break;
                }
                else if (gifState[gifData.name][rawImageHashCode] == State.Playing)
                {
                    #if UNITY_EDITOR
                    Debug.LogWarningFormat("{0} 正在 {1} 上播放！！！", gifData.name, rawImage.name);
                    #endif
                    yield break;
                }
            }

            // 预防重复加载同一 GIF
            if (!gifTextureWarehouse.ContainsKey(gifData.name))
            {
                gifTextureWarehouse[gifData.name] = null;
                gifInitialization[gifData.name] = false;
                yield return StartCoroutine(GetTexture(gifData, rawImageHashCode, filterMode, wrapMode));
            }
            else if (gifTextureWarehouse[gifData.name] == null)
            {
                if (!gifState.ContainsKey(gifData.name)) gifState[gifData.name] = new Dictionary<int, State>();
                gifState[gifData.name][rawImageHashCode] = State.Loading;
                yield return StartCoroutine(WaitForInit(gifData.name));
                gifState[gifData.name][rawImageHashCode] = State.Ready;
            }

            if (gifState[gifData.name][rawImageHashCode] == State.Ready)
            {
                if (!gifCoroutine.ContainsKey(gifData.name)) gifCoroutine[gifData.name] = new Dictionary<int, Coroutine>();
                gifCoroutine[gifData.name][rawImageHashCode] = StartCoroutine(PlayGif(gifData, rawImage, rawImageHashCode));
            }
            else if (gifState[gifData.name][rawImageHashCode] == State.Pause)
            {
                gifState[gifData.name][rawImageHashCode] = State.Playing;
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogError("播放 GIF 失败");
                #endif
            }
        }

        /// <summary>
        /// 详情见内部函数 GifToTexture.GetTextureInternal
        /// </summary>
        private IEnumerator GetTexture(GifData gifData, int rawImageHashCode, FilterMode filterMode, TextureWrapMode wrapMode)
        {
            if (!gifState.ContainsKey(gifData.name)) gifState[gifData.name] = new Dictionary<int, State>();
            gifState[gifData.name][rawImageHashCode] = State.Loading;
            List<GifTexture> gifTextures = null;
            yield return StartCoroutine(GifToTexture.GetTextureInternal(gifData.gifDecoder, (result) => { gifTextures = result; }, filterMode, wrapMode));
            if (gifTextures != null)
            {
                gifTextureWarehouse[gifData.name] = gifTextures;
                gifInitialization[gifData.name] = true;
                gifState[gifData.name][rawImageHashCode] = State.Ready;
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogError("GIF 纹理获取错误。");
                #endif
            }
        }

        /// <summary>
        /// 等待 GIF 初始化（保证 GIF 只初始化一次）
        /// </summary>
        /// <param name="name">GIF 文件名</param>
        /// <returns>迭代器</returns>
        private IEnumerator WaitForInit(string name)
        {
            float lastTime = Time.time;
            int initTime = 10;
            while (!gifInitialization[name])
            {
                yield return null;
                if (Time.time - lastTime > 10)
                {
                    #if UNITY_EDITOR
                    Debug.LogWarningFormat("{0} 初始化过久(已初始化 {1} 秒)", name, initTime);
                    #endif
                    lastTime = Time.time;
                    initTime += 10;
                }
            }
        }

        /// <summary>
        /// 播放 GIF
        /// </summary>
        /// <param name="gifData">GIF 数据</param>
        /// <param name="rawImage">指定 GIF 在哪个 RawImage 上播放</param>
        /// <param name="rawImageHashCode">RawImage 的哈希值</param>
        /// <returns>迭代器</returns>
        private IEnumerator PlayGif(GifData gifData, RawImage rawImage, int rawImageHashCode)
        {
            gifState[gifData.name][rawImageHashCode] = State.Playing;
            gifRawImage[rawImageHashCode] = rawImage;

            int loopCount = gifData.gifDecoder.applicationExtension.loopCount;
            int nowLoopCount = 0;
            int gifTextureIndex = 0;
            float delayTime = -1f;

            while (true)
            {
                switch (gifState[gifData.name][rawImageHashCode])
                {
                    case State.None:
                        yield break;
                    case State.Ready:
                        yield break;
                    case State.Playing:
                        if (delayTime > Time.time)
                        {
                            yield return null;
                            break;
                        }
                        if (gifTextureIndex >= gifTextureWarehouse[gifData.name].Count)
                        {
                            gifTextureIndex = 0;
                            if (loopCount > 0)
                            {
                                ++nowLoopCount;
                                if (nowLoopCount >= loopCount)
                                {
                                    yield break;
                                }
                            }
                        }
                        rawImage.texture = gifTextureWarehouse[gifData.name][gifTextureIndex].texture2d;
                        delayTime = Time.time + gifTextureWarehouse[gifData.name][gifTextureIndex].delayTime;
                        ++gifTextureIndex;
                        yield return null;
                        break;
                    case State.Pause:
                        yield return null;
                        break;
                    default:
                        yield break;
                }
            }
        }

    }
}