using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace mzitu
{
    class UnityModule
    {

        /// <summary>
        /// 文章目录地址
        /// </summary>
        public static readonly string CatalogAddress = "http://www.mzitu.com/all/";
        /// <summary>
        /// 网站地址
        /// </summary>
        public static readonly string WebSite = "http://www.mzitu.com";
        /// <summary>
        /// 数据库文件名称
        /// </summary>
        private static readonly string DBName = "mzitu.mdb";
        /// <summary>
        /// 存档目录名称
        /// </summary>
        private static readonly string CDName = "mzitu";
        /// <summary>
        /// 存档目录路径
        /// </summary>
        public static readonly string ContentDirectory = FileController.PathCombine(Environment.CurrentDirectory, CDName);
        /// <summary>
        /// 数据库路径
        /// </summary>
        public static readonly string DataBasePath = FileController.PathCombine(ContentDirectory, DBName);
        /// <summary>
        /// 新文章计数
        /// </summary>
        public static int NewArchiveCount = 0;

        /// <summary>
        /// 封装的函数以输出调试信息
        /// </summary>
        /// <param name="DebugMessage">调试信息</param>
        static public void DebugPrint(string DebugMessage)
        {
            try
            {
                string LogMessage = string.Format("{0}    {1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), DebugMessage);
                Debug.Print(LogMessage);
            }
            catch { }
        }

        /// <summary>
        /// 封装的函数以输出调试信息
        /// </summary>
        /// <param name="DebugMessage">调试信息</param>
        /// <param name="DebugValue">调试信息的值</param>
        static public void DebugPrint(string DebugMessage, params object[] DebugValue)
        {
            DebugPrint(string.Format(DebugMessage, DebugValue));
        }
    }
}
