using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KAnimGui.Models
{
    // 表示转换的结果
    public class ConversionResult
    {
        // 是否成功
        public bool Success { get; set; }

        // 退出码（0 表示成功）
        public int ExitCode { get; set; }

        // 错误信息（失败时才有值）
        public required string ErrorMessage { get; set; }
    }
}

