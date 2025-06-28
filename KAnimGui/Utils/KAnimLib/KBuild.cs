using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Controls;

namespace KanimLib
{
    /// <summary>
    /// 包含表示动画中使用的精灵层级数据。
    /// </summary>
    public class KBuild
    {
        /// <summary>
        /// 用于识别文件是否包含 build 数据的 4 字符序列。
        /// </summary>
        public const string BUILD_HEADER = @"BILD";

        /// <summary>
        /// Oxygen Not Included 中随附的 build 文件的当前版本号。
        /// </summary>
        public const int CURRENT_BUILD_VERSION = 10;

        /// <summary>
        /// 获取或设置动画的名称。
        /// </summary>
        /// <remarks>此字段似乎未被 ONI 代码使用。</remarks>
        [ReadOnly(true)]
        public string Name
        { get; set; } = "Uninitialized_Name";

        /// <summary>
        /// 获取或设置 build 数据的版本号。
        /// </summary>
        [ReadOnly(true)]
        public int Version
        { get; set; } = CURRENT_BUILD_VERSION;

        /// <summary>
        /// 获取或设置 build 数据中符号的数量。
        /// </summary>
        [ReadOnly(true)]
        public int SymbolCount
        { get; set; } = 0;

        /// <summary>
        /// 获取或设置 build 数据中的总帧数。
        /// </summary>
        [ReadOnly(true)]
        public int FrameCount
        { get; set; } = 0;

        /// <summary>
        /// 此动画所使用的符号列表。
        /// </summary>
        public readonly List<KSymbol> Symbols = new List<KSymbol>();

        /// <summary>
        /// 以 KHash 为索引的符号名称字典。
        /// </summary>
        public readonly Dictionary<int, string> SymbolNames = new Dictionary<int, string>();

        public KBuild()
        { }

        /// <summary>
        /// 获取 build 数据是否发生了需要重新打包纹理的更改。
        /// </summary>
        [Browsable(false)]
        public bool NeedsRepack
        {
            get
            {
                foreach (var symbol in Symbols)
                {
                    if (symbol.NeedsRepack) return true;
                }
                return false;
            }
            set
            {
                foreach (var symbol in Symbols)
                {
                    symbol.NeedsRepack = value;
                }
            }
        }

        /// <summary>
        /// 根据给定的哈希值返回对应的符号名称。
        /// </summary>
        /// <returns>找不到时返回 null。</returns>
        public string GetSymbolName(int hash)
        {
            if (SymbolNames.ContainsKey(hash))
            {
                return SymbolNames[hash];
            }

            return null;
        }

        /// <summary>
        /// 根据名称返回对应的符号。
        /// </summary>
        /// <returns>找不到时返回 null。</returns>
        public KSymbol GetSymbol(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name");

            foreach (var symbol in Symbols)
            {
                if (symbol.Name == name) return symbol;
            }

            return null;
        }

        /// <summary>
        /// 根据哈希值返回对应的符号。
        /// </summary>
        /// <returns>找不到时返回 null。</returns>
        public KSymbol GetSymbol(int hash)
        {
            foreach (var symbol in Symbols)
            {
                if (symbol.Hash == hash) return symbol;
            }

            return null;
        }

        /// <summary>
        /// 根据符号名称和子图像索引返回对应的帧。
        /// </summary>
        /// <returns>找不到时返回 null。</returns>
        public KFrame GetFrame(string name, int index)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("name");

            foreach (var symbol in Symbols)
            {
                if (symbol.Name == name)
                {
                    foreach (var frame in symbol.Frames)
                    {
                        if (frame.Index == index) return frame;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 添加符号到符号列表，并维护相关数据。
        /// </summary>
        internal void AddSymbol(KSymbol symbol)
        {
            symbol.Parent = this;
            Symbols.Add(symbol);
            int hash = symbol.Name.KHash();
            SymbolNames[hash] = symbol.Name;
            SymbolCount = Symbols.Count;
        }
    }
}
