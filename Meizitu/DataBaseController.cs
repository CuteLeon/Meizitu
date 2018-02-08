using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.OleDb;
using System.Threading;

namespace Meizitu
{
    /// <summary>
    /// 数据库处理
    /// </summary>
    public class DataBaseController:IDisposable
    {
        /// <summary>
        /// 数据库长连接
        /// </summary>
        public OleDbConnection DataBaseConnection;

        /// <summary>
        /// 数据库命令
        /// </summary>
        public OleDbCommand DataBaseCommand;
        
        /// <summary>
        /// 建立数据库连接.
        /// </summary>
        public bool CreateConnection(string dataBasePath)
        {
            UnityModule.DebugPrint("正在创建数据库连接...");
            try
            {
                //数据库连接到打开时需要断开重新连接
                if (DataBaseConnection != null && DataBaseConnection.State == ConnectionState.Open)
                {
                    CloseConnection();
                    UnityModule.DebugPrint("已经断开上次未关闭的数据库连接");
                }

                DataBaseConnection = new OleDbConnection();
                DataBaseConnection.ConnectionString = string.Format("Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0}", dataBasePath);
                DataBaseCommand = new OleDbCommand() { Connection=DataBaseConnection};
                DataBaseConnection.Open();

                UnityModule.DebugPrint("数据库连接创建成功！数据库状态：" + DataBaseConnection.State.ToString());
                return (DataBaseConnection?.State== ConnectionState.Open);
            }
            catch (Exception ex)
            {
                UnityModule.DebugPrint("创建数据库数据库连接时失败！" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 读取数据库至数据适配器
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <param name="SQLValue">数据库命令内包含的值</param>
        /// <returns>读取结果</returns>
        public OleDbDataAdapter ExecuteAdapter(string SQLCommand, params object[] SQLValue)
        {
            return ExecuteAdapter(string.Format(SQLCommand, SQLValue));
        }

        /// <summary>
        /// 读取数据库至数据适配器
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <returns>读取结果</returns>
        public OleDbDataAdapter ExecuteAdapter(string SQLCommand)
        {
            if (DataBaseConnection == null)
            {
                UnityModule.DebugPrint("数据库连接未创建，无法读取SQL:" + SQLCommand);
                return null;
            }

            try
            {
                Monitor.Enter(DataBaseCommand);
                OleDbDataAdapter DataAdapter = new OleDbDataAdapter(SQLCommand, DataBaseConnection);
                Monitor.Exit(DataBaseCommand);
                UnityModule.DebugPrint("命令执行成功：" + SQLCommand);
                return DataAdapter;
            }
            catch (Exception ex)
            {
                UnityModule.DebugPrint("读取SQL遇到错误：\n\t" + SQLCommand + "\n\t" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 执行数据库命令
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <param name="SQLValue">数据库命令内包含的值</param>
        /// <returns>读取结果</returns>
        public bool ExecuteNonQuery(string SQLCommand, params object[] SQLValue)
        {
            return ExecuteNonQuery(string.Format(SQLCommand, SQLValue));
        }

        /// <summary>
        /// 执行数据库命令
        /// </summary>
        /// <param name="SQLCommand">数据库命令</param>
        /// <returns>执行结果</returns>
        public bool ExecuteNonQuery(string SQLCommand)
         {
            if (DataBaseConnection == null)
            {
                UnityModule.DebugPrint("数据库连接未创建，无法执行SQL:" + SQLCommand);
                return false;
            }
            if (DataBaseConnection.State != ConnectionState.Open)
            {
                UnityModule.DebugPrint("数据库状态为：" + DataBaseConnection.State.ToString()+"；无法执行SQL:" + SQLCommand);
                return false;
            }

            try
            {
                Monitor.Enter(DataBaseCommand);
                DataBaseCommand.CommandText = SQLCommand;
                DataBaseCommand.ExecuteNonQuery();
                Monitor.Exit(DataBaseCommand);
                UnityModule.DebugPrint("命令执行成功：" + SQLCommand);
                return true;
            }
            catch (Exception ex)
            {
                UnityModule.DebugPrint("执行SQL遇到错误：\n\t" + SQLCommand + "\n\t" + ex.Message);
                return false;
            }
         }

        /// <summary>
        /// 读取数据库
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <param name="SQLValue">数据库命令内包含的值</param>
        /// <returns>读取结果</returns>
        public OleDbDataReader ExecuteReader(string SQLCommand, params object[] SQLValue)
        {
            return ExecuteReader(string.Format(SQLCommand, SQLValue));
        }

        /// <summary>
        /// 读取数据库
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <returns>读取结果</returns>
        public OleDbDataReader ExecuteReader(string SQLCommand)
        {
            if (DataBaseConnection == null)
            {
                UnityModule.DebugPrint("数据库连接未创建，无法读取SQL:" + SQLCommand);
                return null;
            }
            if (DataBaseConnection.State != ConnectionState.Open)
            {
                UnityModule.DebugPrint("数据库状态为：" + DataBaseConnection.State.ToString() + "；无法读取SQL:" + SQLCommand);
                return null;
            }

            try
            {
                Monitor.Enter(DataBaseCommand);
                DataBaseCommand.CommandText = SQLCommand;
                OleDbDataReader DataReader=DataBaseCommand.ExecuteReader();
                Monitor.Exit(DataBaseCommand);
                UnityModule.DebugPrint("命令执行成功：" + SQLCommand);
                return DataReader;
            }
            catch (Exception ex)
            {
                UnityModule.DebugPrint("读取SQL遇到错误：\n\t" + SQLCommand + "\n\t" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 读取数据库返回值第一列第一行
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <param name="SQLValue">数据库命令内包含的值</param>
        /// <returns>读取结果</returns>
        public object ExecuteScalar(string SQLCommand,params object[] SQLValue)
        {
            return ExecuteScalar(string.Format(SQLCommand, SQLValue));
        }

        /// <summary>
        /// 读取数据库返回值第一列第一行
        /// </summary>
        /// <param name="SQLCommand">数据库读取命令</param>
        /// <returns>读取结果</returns>
        public object ExecuteScalar(string SQLCommand)
        {
            if (DataBaseConnection == null)
            {
                UnityModule.DebugPrint("数据库连接未创建，无法读取SQL:" + SQLCommand);
                return null;
            }
            if (DataBaseConnection.State != ConnectionState.Open)
            {
                UnityModule.DebugPrint("数据库状态为：" + DataBaseConnection.State.ToString() + "；无法读取SQL:" + SQLCommand);
                return null;
            }

            try
            {
                Monitor.Enter(DataBaseCommand);
                DataBaseCommand.CommandText = SQLCommand;
                object DataValue = DataBaseCommand.ExecuteScalar();
                Monitor.Exit(DataBaseCommand);
                UnityModule.DebugPrint("命令执行成功：" + SQLCommand);
                return DataValue;
            }
            catch (Exception ex)
            {
                UnityModule.DebugPrint("读取SQL遇到错误：\n\t\t\t" + SQLCommand + "\n\t\t\t" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 判断数据库是否连接
        /// </summary>
        /// <returns></returns>
        public bool IsConnected()
        {
            if (DataBaseConnection == null) return false;
            return (DataBaseConnection.State == ConnectionState.Open ||
                DataBaseConnection.State == ConnectionState.Connecting ||
                DataBaseConnection.State == ConnectionState.Fetching ||
                DataBaseConnection.State == ConnectionState.Executing);
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void CloseConnection()
        {
            UnityModule.DebugPrint("关闭数据库连接...");
            try
            {
                if (DataBaseConnection == null) return;
                DataBaseConnection?.Close();
                DataBaseConnection?.Dispose();
                DataBaseCommand?.Dispose();
                DataBaseConnection = null;
                DataBaseCommand = null;
            }
            catch (Exception ex) {
                UnityModule.DebugPrint("关闭数据库连接时遇到错误：",ex.Message);
            }
        }

        public void Dispose()
        {
            UnityModule.DebugPrint("数据库连接对象释放...");
            CloseConnection();
        }

    }

}
