using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityGif
{
    /// <summary>
    /// GIF 帧纹理
    /// </summary>
    public struct GifTexture
    {
        /// <summary>
        /// 当前帧的纹理
        /// </summary>
        public Texture2D texture2d;
        
        /// <summary>
        /// 渲染下一个纹理的延迟时间
        /// </summary>
        public float delayTime;

        /// <summary>
        /// 构造帧纹理
        /// </summary>
        /// <param name="texture2d">当前帧的纹理</param>
        /// <param name="delayTime">渲染下一个纹理的延迟时间</param>
        public GifTexture(Texture2D texture2d, float delayTime)
        {
            this.texture2d = texture2d;
            this.delayTime = delayTime;
        }
    }

    /// <summary>
    /// 用于序列化List<byte[]>
    /// </summary>
    [Serializable]
    public struct SerializableByteArray
    {
        /// <summary>
        /// 字节数组
        /// </summary>
        public byte[] bytes;

        /// <summary>
        /// 构造字节数组
        /// </summary>
        /// <param name="bytes">字节数组</param>
        public SerializableByteArray(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }

    public class GifToTexture
    {
        /// <summary>
        /// 通过 GIF 的数据获取每帧的纹理
        /// </summary>
        /// <param name="gifDecoder">与 GIF 对应的解码器</param>
        /// <param name="callback">回调函数（用于传出解析好的纹理列表，参数是一个纹理列表）</param>
        /// <param name="filterMode">纹理过滤模式</param>
        /// <param name="wrapMode">纹理包裹模式</param>
        /// <returns>迭代器</returns>
        public static IEnumerator GetTextureInternal(GifDecoder gifDecoder, Action<List<GifTexture>> callback, FilterMode filterMode = FilterMode.Bilinear, TextureWrapMode wrapMode = TextureWrapMode.Clamp)
        {
            if (gifDecoder.imageBlocks == null || gifDecoder.imageBlocks.Count < 1)
            {
                yield break;
            }

            List<GifTexture> gifTextures = new List<GifTexture>(gifDecoder.imageBlocks.Count);
            List<UInt16> disposalMethods = new List<UInt16>(gifDecoder.imageBlocks.Count);

            int imageBlockIndex = 0;
            for (int i = 0; i < gifDecoder.imageBlocks.Count; ++i)
            {
                byte[] decodedData = GetDecodedData(gifDecoder.imageBlocks[i]);

                GifDecoder.GraphicControlExtension? graphicControlExtension = GetGraphicControlExtension(gifDecoder, imageBlockIndex);

                int transparentIndex = GetTransparentIndex(graphicControlExtension);

                disposalMethods.Add(GetDisposalMethod(graphicControlExtension));

                List<SerializableByteArray> colorTable = GetColorTableAndSetBackgroundColor(gifDecoder, gifDecoder.imageBlocks[i], transparentIndex, out Color32 backgroundColor);

                yield return 0;

                Texture2D texture2D = CreateTexture2D(gifDecoder, gifTextures, imageBlockIndex, disposalMethods, backgroundColor, filterMode, wrapMode, out bool filledTexture);

                yield return 0;

                // 设置像素数据
                int dataIndex = 0;
                for (int y = texture2D.height - 1; y >= 0; y--)
                {
                    SetTexturePixelRow(texture2D, y, gifDecoder.imageBlocks[i], decodedData, ref dataIndex, colorTable, backgroundColor, transparentIndex, filledTexture);
                }
                texture2D.Apply();

                yield return 0;

                float delaySecond = GetDelaySecond(graphicControlExtension);

                // Add to GIF texture list
                gifTextures.Add(new GifTexture(texture2D, delaySecond));

                imageBlockIndex++;
            }

            if (callback != null)
            {
                callback(gifTextures);
            }

            yield break;
        }

        /// <summary>
        /// 从 ImageBlock 获取解码图像数据
        /// </summary>
        /// <param name="imgBlock">GIF 的图像块</param>
        /// <returns>解码图像数据</returns>
        private static byte[] GetDecodedData(GifDecoder.ImageBlock imageBlock)
        {
            // 合并 LZW 压缩数据
            List<byte> lzwData = new List<byte>();
            for (int i = 0; i < imageBlock.imageDataBlocks.Count; ++i)
            {
                for (int j = 0; j < imageBlock.imageDataBlocks[i].data.Length; ++j)
                {
                    lzwData.Add(imageBlock.imageDataBlocks[i].data[j]);
                }
            }

            // LZW 解码
            int dataSize = imageBlock.imageWidth * imageBlock.imageHeight;
            byte[] decodeData = DecodeGifLZW(lzwData, imageBlock.lzwMinimumCodeSize, dataSize);

            // 对 GIF 的交插图像（如果有）进行排序
            if (imageBlock.interlaceFlag)
            {
                decodeData = SortInterlaceGifData(decodeData, imageBlock.imageWidth);
            }
            return decodeData;
        }

        /// <summary>
        /// 解码 GIF 的 LZW 压缩数据
        /// </summary>
        /// <param name="lzwData">GIF 所有的 LZW 压缩数据</param>
        /// <param name="lzwMinimumCodeSize">LZW 编码初始表大小的位数</param>
        /// <param name="dataSize">图像块大小</param>
        /// <returns>解码后的数据数组</returns>
        private static byte[] DecodeGifLZW(List<byte> lzwData, int lzwMinimumCodeSize, int dataSize)
        {
            // 初始化字典
            Dictionary<int, string> dic = new Dictionary<int, string>();
            InitDictionary(dic, lzwMinimumCodeSize, out int lzwCodeSize, out int clearCode, out int finishCode);

            // 转换为位数组
            BitArray bitData = new BitArray(lzwData.ToArray());

            byte[] output = new byte[dataSize];

            int outputAddIndex = 0;

            string prevEntry = null;

            bool dicInitFlag = false;

            int bitDataIndex = 0;

            // LZW 循环解码
            while (bitDataIndex < bitData.Length)
            {
                if (dicInitFlag)
                {
                    InitDictionary(dic, lzwMinimumCodeSize, out lzwCodeSize, out clearCode, out finishCode);
                    dicInitFlag = false;
                }

                int key = bitData.GetNumeral(bitDataIndex, lzwCodeSize);

                string entry = null;

                if (key == clearCode)
                {
                    // 初始化字典
                    dicInitFlag = true;
                    bitDataIndex += lzwCodeSize;
                    prevEntry = null;
                    continue;
                }
                else if (key == finishCode)
                {
                    // 退出
                    #if UNITY_EDITOR
                    Debug.LogWarningFormat("提前停止代码。 位数组下表 : {0} LZW 码大小 : {1} 键 : {2} 字典大小: {3}", bitDataIndex, lzwCodeSize, key, dic.Count);
                    #endif
                    break;
                }
                else if (dic.ContainsKey(key))
                {
                    // 输出字典
                    entry = dic[key];
                }
                else if (key >= dic.Count)
                {
                    if (prevEntry != null)
                    {
                        // 输出估算值
                        entry = prevEntry + prevEntry[0];
                    }
                    else
                    {
                        #if UNITY_EDITOR
                        Debug.LogWarningFormat("理论上不应该输出该警告。 位数组下表 : {0} LZW 码大小 : {1} 键 : {2} 字典大小: {3}", bitDataIndex, lzwCodeSize, key, dic.Count);
                        #endif
                        bitDataIndex += lzwCodeSize;
                        continue;
                    }
                }
                else
                {
                    #if UNITY_EDITOR
                    Debug.LogWarningFormat("理论上不应该输出该警告。 位数组下表 : {0} LZW 码大小 : {1} 键 : {2} 字典大小: {3}", bitDataIndex, lzwCodeSize, key, dic.Count);
                    #endif
                    bitDataIndex += lzwCodeSize;
                    continue;
                }

                // 输出
                // 从字符串中取出8位。
                byte[] temp = Encoding.Unicode.GetBytes(entry);
                for (int i = 0; i < temp.Length; ++i)
                {
                    if (i % 2 == 0)
                    {
                        output[outputAddIndex++] = temp[i];
                    }
                }

                if (outputAddIndex >= dataSize)
                {
                    // 退出
                    break;
                }

                if (prevEntry != null)
                {
                    // 加入字典
                    dic.Add(dic.Count, prevEntry + entry[0]);
                }

                prevEntry = entry;

                bitDataIndex += lzwCodeSize;

                if (lzwCodeSize == 3 && dic.Count >= 8)
                {
                    lzwCodeSize = 4;
                }
                else if (lzwCodeSize == 4 && dic.Count >= 16)
                {
                    lzwCodeSize = 5;
                }
                else if (lzwCodeSize == 5 && dic.Count >= 32)
                {
                    lzwCodeSize = 6;
                }
                else if (lzwCodeSize == 6 && dic.Count >= 64)
                {
                    lzwCodeSize = 7;
                }
                else if (lzwCodeSize == 7 && dic.Count >= 128)
                {
                    lzwCodeSize = 8;
                }
                else if (lzwCodeSize == 8 && dic.Count >= 256)
                {
                    lzwCodeSize = 9;
                }
                else if (lzwCodeSize == 9 && dic.Count >= 512)
                {
                    lzwCodeSize = 10;
                }
                else if (lzwCodeSize == 10 && dic.Count >= 1024)
                {
                    lzwCodeSize = 11;
                }
                else if (lzwCodeSize == 11 && dic.Count >= 2048)
                {
                    lzwCodeSize = 12;
                }
                else if (lzwCodeSize == 12 && dic.Count >= 4096)
                {
                    int nextKey = bitData.GetNumeral(bitDataIndex, lzwCodeSize);
                    if (nextKey != clearCode)
                    {
                        dicInitFlag = true;
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// 初始化字典
        /// </summary>
        /// <param name="dic">字典</param>
        /// <param name="lzwMinimumCodeSize">LZW 编码初始表大小的位数</param>
        /// <param name="clearCode">清除码</param>
        /// <param name="finishCode">结束码</param>
        /// <param name="lzwCodeSize">LZW 大小</param>
        private static void InitDictionary(Dictionary<int, string> dic, int lzwMinimumCodeSize, out int lzwCodeSize, out int clearCode, out int finishCode)
        {
            int dicSize = (int)Math.Pow(2, lzwMinimumCodeSize);

            clearCode = dicSize;
            finishCode = clearCode + 1;

            dic.Clear();

            int range = dicSize + 2;
            for (int i = 0; i < range; ++i)
            {
                dic.Add(i, ((char)i).ToString());
            }

            lzwCodeSize = lzwMinimumCodeSize + 1;
        }

        /// <summary>
        /// 对 GIF 的交插图像进行排序
        /// </summary>
        /// <param name="decodedData">解码过的 GIF 数据</param>
        /// <param name="imageWidth">水平行像素数</param>
        /// <returns>已排序数据</returns>
        private static byte[] SortInterlaceGifData(byte[] decodedData, int imageWidth)
        {
            int rowNo = 0;
            int dataIndex = 0;
            byte[] newArr = new byte[decodedData.Length];
            // 第一通道(Pass 1) 提取从第 0 行开始每隔 8 行的数据
            for (int i = 0; i < newArr.Length; i++)
            {
                if (rowNo % 8 == 0)
                {
                    newArr[i] = decodedData[dataIndex];
                    dataIndex++;
                }
                if (i != 0 && i % imageWidth == 0)
                {
                    rowNo++;
                }
            }
            rowNo = 0;
            // 第二通道(Pass 2) 提取从第 4 行开始每隔 8 行的数据；
            for (int i = 0; i < newArr.Length; i++)
            {
                if (rowNo % 8 == 4)
                {
                    newArr[i] = decodedData[dataIndex];
                    dataIndex++;
                }
                if (i != 0 && i % imageWidth == 0)
                {
                    rowNo++;
                }
            }
            rowNo = 0;
            // 第三通道(Pass 3) 提取从第 2 行开始每隔 4 行的数据；
            for (int i = 0; i < newArr.Length; i++)
            {
                if (rowNo % 4 == 2)
                {
                    newArr[i] = decodedData[dataIndex];
                    dataIndex++;
                }
                if (i != 0 && i % imageWidth == 0)
                {
                    rowNo++;
                }
            }
            rowNo = 0;
            // 第四通道(Pass 4) 提取从第 1 行开始每隔 2 行的数据
            for (int i = 0; i < newArr.Length; i++)
            {
                if (rowNo % 8 != 0 && rowNo % 8 != 4 && rowNo % 4 != 2)
                {
                    newArr[i] = decodedData[dataIndex];
                    dataIndex++;
                }
                if (i != 0 && i % imageWidth == 0)
                {
                    rowNo++;
                }
            }

            return newArr;
        }

        /// <summary>
        /// 从 GifDecoder 中获取图形控制扩展
        /// </summary>
        /// <param name="gifDecoder">GIF 解码器</param>
        /// <param name="imageBlockIndex">图像块下标</param>
        /// <returns>图形控制扩展</returns>
        private static GifDecoder.GraphicControlExtension? GetGraphicControlExtension(GifDecoder gifDecoder, int imageBlockIndex)
        {
            if (gifDecoder.graphicControlExtensions != null && gifDecoder.graphicControlExtensions.Count > imageBlockIndex)
            {
                return gifDecoder.graphicControlExtensions[imageBlockIndex];
            }
            return null;
        }

        /// <summary>
        /// 从图形控制扩展中获取透明颜色索引
        /// </summary>
        /// <param name="graphicControlExtension">图形控制扩展</param>
        /// <returns>透明颜色索引</returns>
        private static int GetTransparentIndex(GifDecoder.GraphicControlExtension? graphicControlExtension)
        {
            int transparentIndex = -1;
            if (graphicControlExtension != null && graphicControlExtension.Value.transparentColorFlag)
            {
                transparentIndex = graphicControlExtension.Value.transparentColorIndex;
            }
            return transparentIndex;
        }

        /// <summary>
        /// 从图形控制扩展中获取处理方法（若该 GIF 无图形控制扩展则默认为 2，即恢复到背景色）
        /// </summary>
        /// <param name="graphicControlExtension">图形控制扩展</param>
        /// <returns>处理方法</returns>
        private static UInt16 GetDisposalMethod(GifDecoder.GraphicControlExtension? graphicControlExtension)
        {
            return graphicControlExtension != null ? graphicControlExtension.Value.disposalMethod : (UInt16)2;
        }

        /// <summary>
        /// 获取颜色表并设置背景颜色（本地或全局）
        /// </summary>
        /// <param name="gifDecoder">GIF 解码器</param>
        /// <param name="imageBlock">GIF 图像块</param>
        /// <param name="transparentIndex">透明颜色索引</param>
        /// <param name="backgroundColor">背景色</param>
        /// <returns>颜色表</returns>
        private static List<SerializableByteArray> GetColorTableAndSetBackgroundColor(GifDecoder gifDecoder, GifDecoder.ImageBlock imageBlock, int transparentIndex, out Color32 backgroundColor)
        {
            List<SerializableByteArray> colorTable = imageBlock.localColorTableFlag ? imageBlock.localColorTable : gifDecoder.globalFlag.globalColorTableFlag ? gifDecoder.globalColorTable : null;

            if (colorTable != null)
            {
                // 从颜色表中设置背景颜色
                byte[] backgroundRGB = colorTable[gifDecoder.backgroundColorIndex].bytes;
                backgroundColor = new Color32(backgroundRGB[0], backgroundRGB[1], backgroundRGB[2], (byte)(transparentIndex == gifDecoder.backgroundColorIndex ? 0 : 255));
            }
            else
            {
                backgroundColor = Color.black;
            }

            return colorTable;
        }

        /// <summary>
        /// 创建当前帧 Texture2D 对象和初始设置
        /// </summary>
        /// <param name="gifDecoder">GIF 解码器</param>
        /// <param name="gifTextures">GIF 纹理列表</param>
        /// <param name="imageBlockIndex">GIF 图像块下标</param>
        /// <param name="disposalMethods">图形处理方法</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="filterMode">纹理过滤模式</param>
        /// <param name="wrapMode">纹理包裹模式</param>
        /// <param name="filledTexture">纹理是否填充</param>
        /// <returns>当前帧 Texture2D 对象</returns>
        private static Texture2D CreateTexture2D(GifDecoder gifDecoder, List<GifTexture> gifTextures, int imageBlockIndex, List<UInt16> disposalMethods, Color32 backgroundColor, FilterMode filterMode, TextureWrapMode wrapMode, out bool filledTexture)
        {
            filledTexture = false;

            // 创建纹理
            Texture2D tex = new Texture2D(gifDecoder.logicalScreenWidth, gifDecoder.logicalScreenHeight, TextureFormat.ARGB32, false);
            tex.filterMode = filterMode;
            tex.wrapMode = wrapMode;

            // 检查处理方法
            UInt16 disposalMethod = imageBlockIndex > 0 ? disposalMethods[imageBlockIndex - 1] : (UInt16)2;
            int useBeforeIndex = -1;
            if (disposalMethod == 0)
            {
                // 0 (未指定处置方式)
            }
            else if (disposalMethod == 1)
            {
                // 1 (不处置)
                useBeforeIndex = imageBlockIndex - 1;
            }
            else if (disposalMethod == 2)
            {
                // 2 (恢复为背景色)
                filledTexture = true;
                Color32[] pix = new Color32[tex.width * tex.height];
                for (int i = 0; i < pix.Length; i++)
                {
                    pix[i] = backgroundColor;
                }
                tex.SetPixels32(pix);
                tex.Apply();
            }
            else if (disposalMethod == 3)
            {
                // 3 (恢复到以前)
                for (int i = imageBlockIndex - 1; i >= 0; i--)
                {
                    if (disposalMethods[i] == 0 || disposalMethods[i] == 1)
                    {
                        useBeforeIndex = i;
                        break;
                    }
                }
            }

            if (useBeforeIndex >= 0)
            {
                filledTexture = true;
                Color32[] pix = gifTextures[useBeforeIndex].texture2d.GetPixels32();
                tex.SetPixels32(pix);
                tex.Apply();
            }

            return tex;
        }

        /// <summary>
        /// 设置纹理像素行
        /// </summary>
        /// <param name="texture2D">当前帧纹理</param>
        /// <param name="y">纹理高度</param>
        /// <param name="imageBlock">GIF 图像块</param>
        /// <param name="decodedData">解码图像数据</param>
        /// <param name="dataIndex">数据下标</param>
        /// <param name="colorTable">颜色表</param>
        /// <param name="backgroundColor">背景色</param>
        /// <param name="transparentIndex">透明颜色索引</param>
        /// <param name="filledTexture">是否填充纹理</param>
        private static void SetTexturePixelRow(Texture2D texture2D, int y, GifDecoder.ImageBlock imageBlock, byte[] decodedData, ref int dataIndex, List<SerializableByteArray> colorTable, Color32 backgroundColor, int transparentIndex, bool filledTexture)
        {
            int row = texture2D.height - 1 - y;

            for (int x = 0; x < texture2D.width; x++)
            {
                int line = x;

                // 图像块不足
                if (row < imageBlock.yOffset ||
                    row >= imageBlock.yOffset + imageBlock.imageHeight ||
                    line < imageBlock.xOffset ||
                    line >= imageBlock.xOffset + imageBlock.imageWidth)
                {
                    // 从背景色获取像素颜色
                    if (filledTexture == false)
                    {
                        texture2D.SetPixel(x, y, backgroundColor);
                    }
                    continue;
                }

                // 解码数据不足
                if (dataIndex >= decodedData.Length)
                {
                    if (filledTexture == false)
                    {
                        texture2D.SetPixel(x, y, backgroundColor);
                        if (dataIndex == decodedData.Length)
                        {
                            #if UNITY_EDITOR
                            Debug.LogErrorFormat("dataIndex 超过了 decodedData 的大小。dataIndex:{0} decodedData.Length:{1} y:{2} x:{3}", dataIndex, decodedData.Length, y, x);
                            #endif
                        }
                    }
                    dataIndex++;
                    continue;
                }

                // 从颜色表中获取像素颜色
                {
                    byte colorIndex = decodedData[dataIndex];
                    if (colorTable == null || colorTable.Count <= colorIndex)
                    {
                        if (filledTexture == false)
                        {
                            texture2D.SetPixel(x, y, backgroundColor);
                            if (colorTable == null)
                            {
                                #if UNITY_EDITOR
                                Debug.LogErrorFormat("colorIndex 超出了 colorTable 的大小。colorTable为空。colorIndex:{0}", colorIndex);
                                #endif
                            }
                            else
                            {
                                #if UNITY_EDITOR
                                Debug.LogErrorFormat("colorIndex 超出了 colorTable 的大小。colorTable.Count:{0} colorIndex:{1}", colorTable.Count, colorIndex);
                                #endif
                            }
                        }
                        dataIndex++;
                        continue;
                    }
                    byte[] rgb = colorTable[colorIndex].bytes;

                    // 设置透明度
                    byte alpha = transparentIndex >= 0 && transparentIndex == colorIndex ? (byte)0 : (byte)255;

                    if (filledTexture == false || alpha != 0)
                    {
                        // 设置颜色
                        Color32 col = new Color32(rgb[0], rgb[1], rgb[2], alpha);
                        texture2D.SetPixel(x, y, col);
                    }
                }

                dataIndex++;
            }
        }

        /// <summary>
        /// 从图形控制扩展获取延迟秒数
        /// </summary>
        /// <param name="graphicControlExtension">图形控制扩展</param>
        /// <returns>延迟秒数</returns>
        private static float GetDelaySecond(GifDecoder.GraphicControlExtension? graphicControlExtension)
        {
            float delaySecond = graphicControlExtension != null ? graphicControlExtension.Value.delayTime / 100f : (1f / 60f);
            if (delaySecond <= 0f)
            {
                delaySecond = 0.1f;
            }
            return delaySecond;
        }
    }
}
