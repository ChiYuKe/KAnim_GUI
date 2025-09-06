using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KAnimGui.Models
{
    public static class AppSettings
    {
        public static bool OpenFolderAfterConvert { get; set; } = false;

        public static bool NoSuccessPopup { get; set; } = false;
        

        public static bool EnableTxtToBytes { get; set; }

        public static bool UseCustomKsePath { get; set; } = false; // 是否使用自定义kanimal-cli.exe路径

        public static string CustomKsePath { get; set; } = string.Empty; // 


        public static void ApplyAll()
        {
            OpenFolderAfterConvert = Properties.Default.OpenFolderAfterConvert;
            NoSuccessPopup = Properties.Default.NoSuccessPopup;
            EnableTxtToBytes = Properties.Default.EnableTxtToBytes;
            UseCustomKsePath = Properties.Default.UseCustomKsePath;
            CustomKsePath = Properties.Default.CustomKsePath ?? "";
        }


    }

}
