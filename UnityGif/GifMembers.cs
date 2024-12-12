using System;
using System.Collections.Generic;

namespace UnityGif
{
    [Serializable]
    public partial class GifDecoder
    {
        /// <summary>
        /// 遍历字节数组时的下标
        /// </summary>
        private int byteIndex = 0;

        /// <summary>
        /// GIF的二进制数据
        /// </summary>
        private readonly byte[] bytes;

        /// <summary>
        /// GIF署名
        /// </summary>
        public string signature;

        /// <summary>
        /// GIF版本号（87a或89a）
        /// </summary>
        public string version;

        /// <summary>
        /// GIF的宽度（逻辑显示屏的宽度，以像素为单位）
        /// </summary>
        public UInt16 logicalScreenWidth;

        /// <summary>
        /// GIF的高度（逻辑显示屏的高度，以像素为单位）
        /// </summary>
        public UInt16 logicalScreenHeight;
    
        /// <summary>
        /// 全局标志
        /// </summary>
        [Serializable]
        public struct GlobalFlag
        {
            /// <summary>
            /// 全局颜色列表标志（全局颜色列表是否存在）
            /// </summary>
            public bool globalColorTableFlag;

            /// <summary>
            /// GIF的颜色深度（图像调色板中每个颜色的原色所占用的Bit数）
            /// </summary>
            public UInt16 colorResolution;

            /// <summary>
            /// 颜色列表排序方式（若为0，表示颜色列表是按照颜色在图像中出现的频率升序排列，若为1，降序）
            /// </summary>
            public bool sortFlag;

            /// <summary>
            /// 全局颜色列表大小
            /// </summary>
            public int sizeOfGlobalColorTable;
        }
        public GlobalFlag globalFlag = new GlobalFlag();

        /// <summary>
        /// 背景颜色在全局颜色列表中的索引
        /// </summary>
        public byte backgroundColorIndex;

        /// <summary>
        /// 全局像素的宽度与高度的比值
        /// </summary>
        public byte pixelAspectRatio;
        
        /// <summary>
        /// GIF的全局颜色列表
        /// </summary>
        public List<SerializableByteArray> globalColorTable = null;

        // -------------------- 完成全局配置

        /// <summary>
        /// 图像块
        /// </summary>
        [Serializable]
        public struct ImageBlock
        {
            /// <summary>
            /// 图像标识符分割符（固定为0x2c，即 ',' ）
            /// </summary>
            public byte imageSeparator;

            /// <summary>
            /// X方向偏移量
            /// </summary>
            public UInt16 xOffset;

            /// <summary>
            /// Y方向偏移量
            /// </summary>
            public UInt16 yOffset;

            /// <summary>
            /// 图像宽度
            /// </summary>
            public UInt16 imageWidth;

            /// <summary>
            /// 图像高度
            /// </summary>
            public UInt16 imageHeight;

            /// <summary>
            /// 局部颜色列表标志（若为1，表示有一个局部彩色表（Local Color Table）将紧跟在这个图像描述块（ImageDescriptor）之后；若为0，表示图像描述块（Image Descriptor）后面没有局部彩色表（Local Color Table），该图像要使用全局彩色表（GlobalColor Table））
            /// </summary>
            public bool localColorTableFlag;

            /// <summary>
            /// 交插显示标志（若为0，表示该图像不是交插图像；若为1表示该图像是交插图像。使用该位标志可知道图像数据是如何存放的）
            /// </summary>
            public bool interlaceFlag;

            /// <summary>
            /// 局部颜色排序标志（与全局彩色表（GlobalColor Table）中（Sort Flag）域的含义相同）
            /// </summary>
            public bool sortFlag;

            /// <summary>
            /// 保留（未被使用，必须初始化为0）
            /// </summary>
            public byte reserved;

            /// <summary>
            /// 局部颜色列表大小（用来计算局部彩色表（Global Color Table）中包含的字节数）
            /// </summary>
            public int localColorTableSize;

            /// <summary>
            /// 用于存储 GIF 的局部颜色列表（如果存在的话）
            /// </summary>
            public List<SerializableByteArray> localColorTable;

            /// <summary>
            /// LZW 编码初始表大小的位数
            /// </summary>
            public byte lzwMinimumCodeSize;

            /// <summary>
            /// 用于存储图像块中的数据块
            /// </summary>
            public List<ImageDataBlock> imageDataBlocks;

            /// <summary>
            /// 图像数据（块）
            /// </summary>
            [Serializable]
            public struct ImageDataBlock
            {
                /// <summary>
                /// 块大小，不包括 blockSize 所占的这个字节
                /// </summary>
                public byte blockSize;

                /// <summary>
                /// 块数据，8-bit 的字符串
                /// </summary>
                public byte[] data;
            }
        }

        /// <summary>
        /// 用于存储GIF的图像块（按照帧顺序排列）
        /// </summary>
        public List<ImageBlock> imageBlocks = null;

        // -------------------- 完成图像块配置

        /// <summary>
        /// 图形控制扩展
        /// </summary>
        [Serializable]
        public struct GraphicControlExtension
        {
            /// <summary>
            /// 标识这是一个扩展块，固定值 0x21
            /// </summary>
            public byte extensionIntroducer;

            /// <summary>
            /// 标识这是一个图形控制扩展块，固定值 0xF9
            /// </summary>
            public byte GraphicControlLabel;

            /// <summary>
            /// 图形控制扩展块大小，不包括块终结器，固定值 4
            /// </summary>
            public byte blockSize;

            /// <summary>
            /// `0` - 不使用处置方法；
            /// `1` - 不处置图形，把图形从当前位置移去；
            /// `2` - 恢复到背景色；
            /// `3` - 恢复到先前状态；
            /// `4 ~ 7` - 未定义
            /// </summary>
            public UInt16 disposalMethod;

            /// <summary>
            /// 用户输入标志，值为真表示期待，值为否表示不期待。
            /// </summary>
            public bool userInputFlag;

            /// <summary>
            /// 透明颜色标志，值为 1 表示使用透明颜色
            /// </summary>
            public bool transparentColorFlag;

            /// <summary>
            /// 单位 1/100 秒，如果值不为 1 ，表示暂停规定的时间后再继续往下处理数据流
            /// </summary>
            public UInt16 delayTime;

            /// <summary>
            /// 透明色索引值
            /// </summary>
            public byte transparentColorIndex;

            /// <summary>
            /// 标识块终结，固定值 0
            /// </summary>
            public byte blockTerminator;
        }

        /// <summary>
        /// 用于存储GIF的图形控制扩展（按照帧顺序排列）
        /// </summary>
        public List<GraphicControlExtension> graphicControlExtensions = null;

        /// <summary>
        /// 注释扩展块
        /// </summary>
        [Serializable]
        public struct CommentExtension
        {
            /// <summary>
            /// 标识符，固定值 0x21
            /// </summary>
            public byte extensionIntroducer;

            /// <summary>
            /// 注释标签，固定值 0xfe
            /// </summary>
            public byte commentLabel;

            /// <summary>
            /// 块结束符，固定值 0x00
            /// </summary>
            public byte blockTerminator;

            /// <summary>
            /// 注释数据块列表
            /// </summary>
            public List<CommentDataBlock> commentDataBlocks;

            /// <summary>
            /// 注释扩展块中的注释数据块
            /// </summary>
            [Serializable]
            public struct CommentDataBlock
            {
                /// <summary>
                /// 注释数据大小
                /// </summary>
                public byte blockSize;
                /// <summary>
                /// 注释数据
                /// </summary>
                public byte[] commentData;
            }
        }

        /// <summary>
        /// 用于存储GIF的注释扩展块（按照帧顺序排列）
        /// </summary>
        public List<CommentExtension> commentExtensions = null;

        /// <summary>
        /// 无格式文本扩展块（图像说明扩充块）
        /// </summary>
        [Serializable]
        public struct PlainTextExtension
        {
            /// <summary>
            /// 扩展标识符，固定值 0x21
            /// </summary>
            public byte extensionIntroducer;
            
            /// <summary>
            /// 无格式文本标识符，固定值 0x01
            /// </summary>
            public byte plainTextLabel;
            
            /// <summary>
            /// 块大小
            /// </summary>
            public byte blockSize;

            /// <summary>
            /// 块结束符，固定值 0x00
            /// </summary>
            public byte blockTerminator;
            
            /// <summary>
            /// 无格式文本数据块列表
            /// </summary>
            public List<PlainTextDataBlock> plainTextDataBlocks;

            /// <summary>
            /// 无格式文本数据块
            /// </summary>
            [Serializable]
            public struct PlainTextDataBlock
            {
                /// <summary>
                /// 块大小
                /// </summary>
                public byte blockSize;
                
                /// <summary>
                /// 无格式文本数据
                /// </summary>
                public byte[] plainTextData;
            }
        }

        /// <summary>
        /// 无格式文本扩展块列表
        /// </summary>
        public List<PlainTextExtension> plainTextExtensions = null;

        /// <summary>
        /// 应用扩展块
        /// </summary>
        [Serializable]
        public struct ApplicationExtension
        {
            /// <summary>
            /// 扩展标识符，固定值 0x21
            /// </summary>
            public byte extensionIntroducer;
            
            /// <summary>
            /// 应用扩展块标识符，固定值 0xFF
            /// </summary>
            public byte extensionLabel;
            
            /// <summary>
            /// 块大小，固定值 0x0b（12）
            /// </summary>
            public byte blockSize;
            
            /// <summary>
            /// 应用程序标识符
            /// </summary>
            public string applicationIdentifier;
            
            /// <summary>
            /// 应用程序识别码
            /// </summary>
            public string applicationAuthenticationCode;

            /// <summary>
            /// 块结束符，固定值 0x00
            /// </summary>
            public byte blockTerminator;
            
            /// <summary>
            /// 应用程序数据块列表
            /// </summary>
            public List<ApplicationDataBlock> applicationDataBlocks;

            /// <summary>
            /// GIF 循环次数（0 表示无限）
            /// </summary>
            public int loopCount;

            /// <summary>
            /// 应用程序数据块
            /// </summary>
            [Serializable]
            public struct ApplicationDataBlock
            {
                /// <summary>
                /// 块大小
                /// </summary>
                public byte blockSize;
                
                /// <summary>
                /// 应用程序数据
                /// </summary>
                public byte[] applicationData;
            }
        }
        public ApplicationExtension applicationExtension = new ApplicationExtension();

        /// <summary>
        /// 标识 GIF 文件结束，固定值 0x3b
        /// </summary>
        private byte trailer;
    }
}
