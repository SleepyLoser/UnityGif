using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGif
{
    public partial class GifDecoder
    {
        /// <summary>
        /// GifDecoder的构造函数
        /// </summary>
        /// <param name="gifData">GifData类型的数据</param>
        /// <param name="bytes">GIF的二进制数据</param>
        public GifDecoder(byte[] bytes)
        {
            this.bytes = bytes;
            GifDecode();
        }

        /// <summary>
        /// GIF解码入口
        /// </summary>
        void GifDecode()
        {
            GetHeader();
            AnalyzeLogicalScreenDescriptor();
            GetGlobalColorTable();
            // -------------------- 完成全局配置
            GetGifBlock();
        }

        

        /// <summary>
        /// 获取GIF署名（Signature）和版本号（Version）
        /// </summary>
        /// <returns>该二进制数据是否为GIF的二进制数据</returns>
        bool GetHeader()
        {
            StringBuilder sb = new StringBuilder();
            for (int index = 0; index < 6; ++index)
            {
                sb.Append(Convert.ToChar(bytes[index]));
            }
            string header = sb.ToString();
            signature = header.Substring(0, 3);
            version = header.Substring(3, 3);
            if (signature.Equals("GIF"))
            {
                #if UNITY_EDITOR
                UnityEngine.Debug.Log("成功解析GIF文件头");
                #endif
                return true;
            }
            return false;
        }

        /// <summary>
        /// 解析逻辑屏幕标识符
        /// </summary>
        void AnalyzeLogicalScreenDescriptor()
        {
            logicalScreenWidth = BitConverter.ToUInt16(bytes, 6);
            logicalScreenHeight = BitConverter.ToUInt16(bytes, 8);

            byte packedByte = bytes[10];
            globalFlag.globalColorTableFlag = (packedByte & 0x80) != 0;
            globalFlag.colorResolution = (UInt16)(((packedByte & 0b01110000) >> 4) + 1);
            globalFlag.sortFlag = (packedByte & 0b00001000) != 0;
            globalFlag.sizeOfGlobalColorTable = (int)Math.Pow(2, (packedByte & 7) + 1);

            backgroundColorIndex = bytes[11];
            pixelAspectRatio = bytes[12];
            #if UNITY_EDITOR
            UnityEngine.Debug.Log("成功解析GIF逻辑屏幕标识符");
            #endif
        }

        /// <summary>
        /// 获取GIF的全局颜色列表
        /// </summary>
        void GetGlobalColorTable()
        {
            if (!globalFlag.globalColorTableFlag)
            {
                #if UNITY_EDITOR
                UnityEngine.Debug.LogWarning("该GIF不包含全局颜色列表");
                #endif
                return;
            }

            globalColorTable = new List<SerializableByteArray>();
            try
            {
                int end = 13 + globalFlag.sizeOfGlobalColorTable * 3;
                for (int index = 13; index < end;)
                {
                    globalColorTable.Add(new SerializableByteArray(new byte[3]{ bytes[index++], bytes[index++], bytes[index++] }));
                }
                byteIndex = end;
                #if UNITY_EDITOR
                UnityEngine.Debug.Log("成功解析GIF全局颜色列表");
                #endif
            }
            catch (Exception e)
            {
                #if UNITY_EDITOR
                UnityEngine.Debug.LogError("获取全局颜色列表失败：" + e.Message);
                #endif
                throw;
            }
        }

        /// <summary>
        /// 获取GIF的其余各个模块
        /// </summary>
        void GetGifBlock()
        {
            try
            {
                int lastIndex, nowIndex;
                while(true)
                {
                    lastIndex = byteIndex;
                    nowIndex = byteIndex;

                    // 图像标识符分割符（固定为0x2c，即 ',' ）
                    if (bytes[nowIndex] == 0x2c)
                    {
                        GetImageBlock(ref byteIndex);
                    }
                    // 扩展块标识，固定值0x21
                    else if (bytes[nowIndex] == 0x21)
                    {
                        switch (bytes[nowIndex + 1])
                        {
                            case 0xf9:
                                GetGraphicControlExtension(ref byteIndex);
                                break;
                            case 0xfe:
                                GetCommentExtension(ref byteIndex);
                                break;
                            case 0x01:
                                GetPlainTextExtension(ref byteIndex);
                                break;
                            case 0xff:
                                GetApplicationExtension(ref byteIndex);
                                break;
                            default:
                                break;
                        }
                    }
                    // GIF文件结束标识符，固定值0x3b
                    else if (bytes[nowIndex] == 0x3b)
                    {
                        trailer = bytes[byteIndex++];
                        break;
                    }
                    if (lastIndex == byteIndex)
                    {
                        throw new Exception("无限循环错误");
                    }
                }
            }
            catch (Exception e)
            {
                #if UNITY_EDITOR
                UnityEngine.Debug.LogError(e.Message);
                #endif
                throw;
            }
            #if UNITY_EDITOR
            UnityEngine.Debug.Log("成功解析所有图像块");
            #endif
        }

        /// <summary>
        /// 获取GIF的图像块
        /// </summary>
        void GetImageBlock(ref int byteIndex)
        {
            ImageBlock imageBlock = new ImageBlock();
            imageBlock.imageSeparator = bytes[byteIndex++];

            imageBlock.xOffset = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            imageBlock.yOffset = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            imageBlock.imageWidth = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            imageBlock.imageHeight = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;
            
            byte packedByte = bytes[byteIndex++];
            imageBlock.localColorTableFlag = (packedByte & 0x80) != 0;
            imageBlock.interlaceFlag = (packedByte & 0x40) != 0;
            imageBlock.sortFlag = (packedByte & 0x20) != 0;
            imageBlock.reserved = 0;
            imageBlock.localColorTableSize = (int)Math.Pow(2, (packedByte & 0x07) + 1);

            if (imageBlock.localColorTableFlag)
            {
                imageBlock.localColorTable = new List<SerializableByteArray>();
                int end = byteIndex + (imageBlock.localColorTableSize * 3);
                for (int index = byteIndex; index < end;)
                {
                    imageBlock.localColorTable.Add(new SerializableByteArray(new byte[3]{ bytes[index++], bytes[index++], bytes[index++] }));
                }
                byteIndex = end;
            }

            imageBlock.lzwMinimumCodeSize = bytes[byteIndex++];

            // 图像数据块，如果需要可重复多次
            while (true)
            {
                byte blockSize = bytes[byteIndex++];
                if (blockSize == 0x00)
                {
                    // #if UNITY_EDITOR
                    // UnityEngine.Debug.Log("基于颜色列表的图像数据为空");
                    // #endif
                    break;
                }

                ImageBlock.ImageDataBlock imageDataBlock = new ImageBlock.ImageDataBlock();
                imageDataBlock.blockSize = blockSize;

                imageDataBlock.data = new byte[blockSize];
                for (int i = 0; i < blockSize; ++i)
                {
                    imageDataBlock.data[i] = bytes[byteIndex++];
                }

                if (imageBlock.imageDataBlocks == null)
                {
                    imageBlock.imageDataBlocks = new List<ImageBlock.ImageDataBlock>();
                }
                imageBlock.imageDataBlocks.Add(imageDataBlock);
            }
            
            if (imageBlocks == null)
            {
                imageBlocks = new List<ImageBlock>();
            }
            imageBlocks.Add(imageBlock);
        }

        /// <summary>
        /// 获取图形控制扩展
        /// </summary>
        void GetGraphicControlExtension(ref int byteIndex)
        {
            GraphicControlExtension graphicControlExtension = new GraphicControlExtension();

            graphicControlExtension.extensionIntroducer = bytes[byteIndex++];
            graphicControlExtension.GraphicControlLabel = bytes[byteIndex++];
            graphicControlExtension.blockSize = bytes[byteIndex++];

            graphicControlExtension.disposalMethod = (UInt16)((bytes[byteIndex] & 28) >> 2);
            graphicControlExtension.userInputFlag = (bytes[byteIndex] & 2) != 0;
            graphicControlExtension.transparentColorFlag = (bytes[byteIndex++] & 1) != 0;

            graphicControlExtension.delayTime = BitConverter.ToUInt16(bytes, byteIndex);
            byteIndex += 2;

            graphicControlExtension.transparentColorIndex = bytes[byteIndex++];

            graphicControlExtension.blockTerminator = bytes[byteIndex++];

            if (graphicControlExtensions == null)
            {
                graphicControlExtensions = new List<GraphicControlExtension>();
            }
            graphicControlExtensions.Add(graphicControlExtension);
        }

        /// <summary>
        /// 获取注释扩展块
        /// </summary>
        void GetCommentExtension(ref int byteIndex)
        {
            CommentExtension commentExtension = new CommentExtension();

            commentExtension.extensionIntroducer = bytes[byteIndex++];
            commentExtension.commentLabel = bytes[byteIndex++];

            while (true)
            {
                // 块结束符 0
                if (bytes[byteIndex] == 0x00)
                {
                    commentExtension.blockTerminator = 0;
                    ++byteIndex;
                    break;
                }

                CommentExtension.CommentDataBlock commentDataBlock = new CommentExtension.CommentDataBlock();
                commentDataBlock.blockSize = bytes[byteIndex++];

                commentDataBlock.commentData = new byte[commentDataBlock.blockSize];
                for (int i = 0; i < commentDataBlock.blockSize; ++i)
                {
                    commentDataBlock.commentData[i] = bytes[byteIndex++];
                }

                if (commentExtension.commentDataBlocks == null)
                {
                    commentExtension.commentDataBlocks = new List<CommentExtension.CommentDataBlock>();
                }
                commentExtension.commentDataBlocks.Add(commentDataBlock);
            }

            if (commentExtensions == null)
            {
                commentExtensions = new List<CommentExtension>();
            }
            commentExtensions.Add(commentExtension);
        }

        /// <summary>
        /// 获取无格式文本扩展块（图像说明扩充块）
        /// </summary>
        void GetPlainTextExtension(ref int byteIndex)
        {
            PlainTextExtension plainTextExtension = new PlainTextExtension();

            plainTextExtension.extensionIntroducer = bytes[byteIndex++];
            plainTextExtension.plainTextLabel = bytes[byteIndex++];
            plainTextExtension.blockSize = bytes[byteIndex++];

            // Text Grid Left Position(2 Bytes) 不支持
            byteIndex += 2;
            // Text Grid Top Position(2 Bytes) 不支持
            byteIndex += 2;
            // Text Grid Width(2 Bytes) 不支持
            byteIndex += 2;
            // Text Grid Height(2 Bytes) 不支持
            byteIndex += 2;
            // Character Cell Width(1 Bytes) 不支持
            ++byteIndex;
            // Character Cell Height(1 Bytes) 不支持
            ++byteIndex;
            // Text Foreground Color Index(1 Bytes) 不支持
            ++byteIndex;
            // Text Background Color Index(1 Bytes) 不支持
            ++byteIndex;

            while (true)
            {
                if (bytes[byteIndex] == 0x00)
                {
                    plainTextExtension.blockTerminator = 0;
                    ++byteIndex;
                    break;
                }
                PlainTextExtension.PlainTextDataBlock plainTextDataBlock = new PlainTextExtension.PlainTextDataBlock();
                plainTextDataBlock.blockSize = bytes[byteIndex++];

                plainTextDataBlock.plainTextData = new byte[plainTextDataBlock.blockSize];
                for (int i = 0; i < plainTextDataBlock.blockSize; ++i)
                {
                    plainTextDataBlock.plainTextData[i] = bytes[byteIndex++];
                }

                if (plainTextExtension.plainTextDataBlocks == null)
                {
                    plainTextExtension.plainTextDataBlocks = new List<PlainTextExtension.PlainTextDataBlock>();
                }
                plainTextExtension.plainTextDataBlocks.Add(plainTextDataBlock);
            }

            if (plainTextExtensions == null)
            {
                plainTextExtensions = new List<PlainTextExtension>();
            }
            plainTextExtensions.Add(plainTextExtension);
        }

        /// <summary>
        /// 获取应用扩展块
        /// </summary>
        void GetApplicationExtension(ref int byteIndex)
        {
            StringBuilder sb = new StringBuilder();

            applicationExtension.extensionIntroducer = bytes[byteIndex++];
            applicationExtension.extensionLabel = bytes[byteIndex++];
            applicationExtension.blockSize = bytes[byteIndex++];

            for (int i = 0; i < 8; ++i)
            {
                sb.Append(bytes[byteIndex++]);
            }
            applicationExtension.applicationIdentifier = sb.ToString();
            sb.Clear();

            for (int i = 0; i < 3; ++i)
            {
                sb.Append(bytes[byteIndex++]);
            }
            applicationExtension.applicationAuthenticationCode = sb.ToString();

            while (true)
            {
                if (bytes[byteIndex] == 0x00)
                {
                    applicationExtension.blockTerminator = 0;
                    ++byteIndex;
                    break;
                }
                
                ApplicationExtension.ApplicationDataBlock applicationDataBlock = new ApplicationExtension.ApplicationDataBlock();
                applicationDataBlock.blockSize = bytes[byteIndex++];

                applicationDataBlock.applicationData = new byte[applicationDataBlock.blockSize];
                for (int i = 0; i < applicationDataBlock.blockSize; ++i)
                {
                    applicationDataBlock.applicationData[i] = bytes[byteIndex++];
                }

                if (applicationExtension.applicationDataBlocks == null)
                {
                    applicationExtension.applicationDataBlocks = new List<ApplicationExtension.ApplicationDataBlock>();
                }
                applicationExtension.applicationDataBlocks.Add(applicationDataBlock);
            }

            if (applicationExtension.applicationDataBlocks == null || applicationExtension.applicationDataBlocks.Count < 1 ||
                applicationExtension.applicationDataBlocks[0].applicationData.Length < 3 ||
                applicationExtension.applicationDataBlocks[0].applicationData[0] != 0x01)
            {
                applicationExtension.loopCount = 0;
            }
            else
            {
                applicationExtension.loopCount = BitConverter.ToUInt16(applicationExtension.applicationDataBlocks[0].applicationData, 1);
            }
        }
    }
}
