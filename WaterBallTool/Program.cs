using System.CommandLine;
namespace WaterBallTool
{

    class Program
    {

        //日志打印队列化处理
        static List<(string, WriteTy, Exception?, double)> WriteLineList = new ();
        public static bool WriteLineing = true;
        static int Main(string[] args)
        {
            var rootCommand = new RootCommand("水球工具");
            rootCommand.AddCommand(FileFinder.GetCommand());//文件搜索器
            rootCommand.AddCommand(WBFilesPack.GetCommand());//水球文件包操作器

            //打印日志线程
            Task logTask = new (() => {
                    while (WriteLineing || WriteLineList.Count > 0)
                    {
                        if(WriteLineList.Count > 0)
                        {
                            try
                            {
                                (string info, WriteTy Ty, Exception? ex, double Progress) = WriteLineList[0];
                                mWriteLine(info, Ty, ex, Progress);
                                if(Ty == WriteTy.Progress) Thread.Sleep(1000 / 5);//进度增加延时
                                WriteLineList.RemoveAt(0);
                            }
                            catch { }
                        }
                        //对进度进行处理，使其进度最低存在一个，且必须处于最后
                        for (int i = 0; WriteLineList.Count > i; i++)
                        {
                            if(i+1 < WriteLineList.Count)
                            {
                                (_,WriteTy Ty,_,_) = WriteLineList[i];
                                if(Ty == WriteTy.Progress)
                                {
                                  WriteLineList.RemoveAt(i);
                                  i--;//因指定索引的项目被移除，进行索引校准，使其不跳过下一个
                                }
                            }
                        }
                        
                    }
                });
            logTask.Start();

            // 开始解析和执行命令
            int returnInt = rootCommand.Invoke(args);
            WriteLineing = false;

            //等待日志打印任务结束
            logTask.Wait();
            return returnInt;
        }
        
        static int WriteLine_errorCount = 0;
        static int WriteLine_warnCount = 0;
        static int WriteLine_readCount = 0;
        public enum WriteTy { None, Info, Warn, Error, Progress, Read }
        public static void WriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
        {
            WriteLineList.Add((info, Ty, ex, Progress));
            WriteLineing = true;
                
        }
         static void mWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
        {
            //类型无(None)或输入(Read),不进行任何处理，直接输出
            if (Ty == WriteTy.None)
            {
                if (Ty == WriteTy.None) Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(info);
            }
            else
            {
                string Str = "";
                int PositionTop = Console.CursorTop;
                int BufferHeight = Console.BufferHeight;
                int BufferWidth = Console.BufferWidth;
                Str += $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff}]";//时间
                //打印类型名称
                switch (Ty)
                {
                    case WriteTy.Info:
                        Console.ForegroundColor = ConsoleColor.White;
                        Str += "[Info]";
                        break;
                    case WriteTy.Warn:
                        WriteLine_warnCount++;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Str += $"[Warn][{WriteLine_warnCount}]";
                        break;
                    case WriteTy.Error:
                        WriteLine_errorCount++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Str += $"[Error][{WriteLine_errorCount}]";
                        break;
                    case WriteTy.Progress:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Str += "[Progress]";
                        break;
                    case WriteTy.Read:
                        WriteLine_readCount++;
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Str += $"[Read][{WriteLine_readCount}]";
                        break;
                    default:
                        break;
                }
                //进度类型时的区别
                if (Ty == WriteTy.Progress)
                {
                    int totalWidth = Console.WindowWidth - Str.Length - 9; // 保留右侧百分比显示区域
                    int progressWidth = (int)(totalWidth * (Progress / 100));
                    string progressBar = new string('>', progressWidth).PadRight(totalWidth, '-');
                    Str += $"[{progressBar}]{Progress,6:0.##}%";
                }
                Console.SetCursorPosition(0, PositionTop);//光标置左
                //防止单行存在多余内容
                if (Ty == WriteTy.Progress)
                {
                    if (info == null) info = "";
                    int StrLength = Str.Length + (int)(info.Length * 1.5);//文本长度
                    int StrHeight = (StrLength  / BufferWidth) +1;//文本占用高度
                    Console.Write(new string(' ', (StrHeight * BufferWidth)));
                    ResPosition();
                    //消息
                    Str += $"{info}  ";
                    Console.Write(Str);//打印
                    ResPosition();
                    void ResPosition()
                    {
                        //校准行数
                        if(PositionTop+1 == BufferHeight)
                        {
                            PositionTop = BufferHeight - StrHeight;
                        }
                        Console.SetCursorPosition(0, PositionTop);//光标置左
                    }
                }
                else
                {
                    //消息
                    Str += $"{info}  ";
                    Console.Write(Str);//打印
                }
            }
            if (Ty != WriteTy.Progress)
            {
                //提供错误对象时打印（进度除外）
                if (ex != null)
                {

                    TyColor(Ty);
                    Console.Write("\nError：");//打印
                    TyColor(WriteTy.Info);
                    Console.Write($"[HResult：{ex.HResult}]");//打印

                    TyColor(Ty);
                    Console.Write("\nMessage：");//打印
                    TyColor(WriteTy.Info);
                    Console.Write(ex.Message);//打印

                    TyColor(Ty);
                    Console.Write("\nSource：");//打印
                    TyColor(WriteTy.Info);
                    Console.Write(ex.Source);//打印

                    TyColor(Ty);
                    Console.Write("\nTargetSite：");//打印
                    TyColor(WriteTy.Info);
                    Console.Write(ex.TargetSite);//打印

                    TyColor(Ty);
                    Console.Write("\nStackTrace：\n");//打印
                    TyColor(WriteTy.Info);
                    Console.WriteLine(ex.StackTrace);//打印
                    //Str += $"| Error [{ex.HResult}] :\nMessage：{ex.Message} \nSource : {ex.Source}\nTargetSite : {ex.TargetSite}\nStackTrace :\n {ex.StackTrace}\n";

                    //打印的颜色
                    static void TyColor(WriteTy Ty)
                    {
                        switch (Ty)
                        {
                            case WriteTy.Info:
                                Console.ForegroundColor = ConsoleColor.White;
                                break;
                            case WriteTy.Warn:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                break;
                            case WriteTy.Error:
                                Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case WriteTy.Progress:
                                Console.ForegroundColor = ConsoleColor.Green;
                                break;
                            case WriteTy.Read:
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                break;
                            default:
                                break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine();//打印一行
                }
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}