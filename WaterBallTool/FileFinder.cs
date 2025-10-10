
using Newtonsoft.Json;
using System.CommandLine;
using static WaterBallTool.Program;

namespace WaterBallTool
{
    public class FileFinder
    {
        public static Command GetCommand()
        {
            //参数
            var inPathArgument = new Argument<string>(name: "文件夹路径", description: "搜索的文件夹");
            var outFileArgument = new Argument<string>(name: "保存路径", description: "保存结果的文件");
            var jsonFormatOption = new Option<bool>(name: "--f", description: "格式化文本");
            //命令
            var ffCommand = new Command("ff", "文件搜索器")
                    {
                        inPathArgument,
                        outFileArgument,
                        jsonFormatOption
                    };
            //执行
            ffCommand.SetHandler((inPath, outFile, jsonFo) =>
            {
                if (Directory.Exists(inPath))
                {
                    WriteLine("开始搜索");
                    long startTime = DateTime.Now.Ticks;
                    WBFilesList filesList = new();
                    filesList.SearchFiles(inPath, false);

                    TimeSpan timeSpan = new(DateTime.Now.Ticks - startTime);
                    WriteLine($"搜索完成：文件数：{filesList.FilesCount}，大小: {WBFilesPack.DataLengthToString(filesList.DataLength)}，耗时:{timeSpan}。正在转换数据并保存");
                    try
                    {
                        // 将文本内容保存到文件
                        using (FileStream fileStream = new($"{outFile}.json", FileMode.Create, FileAccess.ReadWrite))
                        {
                            WriteLine($"文本已成功保存到{outFile}，大小:{WBFilesPack.DataLengthToString(filesList.WriterToFile(fileStream, jsonFo ? Formatting.Indented : Formatting.None))}");
                        }

                        WriteLine("按任意键关闭");
                        _ = Console.Read();
                        
                    }
                    catch (Exception ex)
                    {
                        // 如果保存时发生错误，输出错误信息
                        WriteLine($"保存文本时发生错误", WriteTy.Error, ex);
                        
                    }
                }
                else
                {
                    WriteLine("路径无效，请输入有效的路径。", WriteTy.Error);
                }
            }, inPathArgument, outFileArgument, jsonFormatOption);
            return ffCommand;
        }

        private static void WriteLine(string info, WriteTy ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
        {
            Program.WriteLine($"[文件搜索器]{info}", ty, ex, Progress);
        }

        

        // 定义一个表示文件信息的类
        public class WBFileInfo
        {
            //名称
            public string Name { get; set; } = "";
            //长度
            public long Length { get; set; } = 0;
            //是文件夹
            public bool IsD { get; set; } = false;
        }

         //包文件专用
        public class WBFileInfo2 : WBFileInfo
        {
            //数据起始位置
            public long DataStartPosition { get; set; } = 0;
            //哈希
            public string Hash { get; set; } = "";
            /*===V2===*/
            //数据分段
            public bool IsDataSegmented { get; set; } = false;

            //数据分段时表示数据位置用的，修改文件后用
            public List<long> DataPosition { get; set; } = [];
        }

        public class WBFilesInfo : WBFileInfo
        {
            //当是文件夹时的子文件集合
            public List<WBFileInfo> FilesList { get; set; } = [];
            //所有子文件数(不包括文件夹)
            public int FilesCount { get; set; } = 0;

        }

        // 定义一个表示文件搜索结果列表的类
        public class WBFilesList
        {
            //文件路径
            public string FilesPath { get; set; } = "";
            //文件列表
            public List<WBFileInfo> FilesList { get; set; } = [];
            //总数据大小
            public long DataLength { get; set; } = 0;
            //总文件大小
            public int FilesCount
            {
                get
                {
                    int Count = 0;
                    foreach (var item in FilesList)
                    {
                        //如果是文件夹就加子文件数，否则加1
                        Count += item.IsD ? ((WBFilesInfo)item).FilesCount : 1;
                    }
                    return Count;
                }
            }

            //数据位置
            long dataPosition = 0;
            //过去发生错误
            public bool pastErro = false;

            long taskCount = 0;
            // 搜索文件的公共方法，参数：路径，用于包文件，启用哈希记录
            public void SearchFiles(string path, bool isPack, bool onHash0 = false)
            {
                try
                {
                    FilesPath = (new DirectoryInfo(path)).FullName;
                    DataLength = 0;
                    pastErro = false;
                    dataPosition = 0;

                    //进度更新线程
                    bool Searching = true;
                    Task.Run(() => {
                        while (Searching)
                        {
                            try
                            {
                                FFWriteLine($"正在进行搜索，使用线程数：{taskCount}", WriteTy.Progress);
                                Thread.Sleep(10); 
                            }
                            catch { }
                        }
                    });
                    SearchFilesRecursively(path, isPack, onHash0, FilesList);//进行搜索
                    Searching = false;
                    //Pack处理
                    if (isPack)
                    {
                        PackData(FilesList);
                        DataLength = dataPosition;
                    }
                    else//非Pack仅计算大小
                    {
                        AllDataLength(FilesList);
                    }

                    //Pack数据
                    void PackData(List<WBFileInfo> WPFilesList)
                    {
                        foreach (var wpFileInfo in WPFilesList)
                        {
                            //文件
                            if (!wpFileInfo.IsD)
                            {
                                var wpFileInfo2 = (WBFileInfo2)wpFileInfo;
                                //初始化哈希值的文本，以便之后可以直接保存到文件
                                if (onHash0) wpFileInfo2.Hash = new string('0', 64);

                                //算出文件的數據起始位置
                                wpFileInfo2.DataStartPosition = dataPosition;
                                dataPosition += wpFileInfo.Length;
                            }
                            else
                            {
                                //算出文件的數據起始位置
                                PackData(((WBFilesInfo)wpFileInfo).FilesList);
                            }
                        }
                    }

                    //计算总大小
                    void AllDataLength(List<WBFileInfo> WPFilesList)
                    {
                        foreach (var wpFileInfo in WPFilesList)
                        {
                            //文件
                            if (!wpFileInfo.IsD)
                            {
                                DataLength += wpFileInfo.Length;
                            }
                            else
                            {
                                //算出文件的數據起始位置
                                AllDataLength(((WBFilesInfo)wpFileInfo).FilesList);
                            }
                        }
                    }

                }
                catch (UnauthorizedAccessException uae)
                {
                    pastErro = true;
                    FFWriteLine("拒绝访问", WriteTy.Warn, uae);
                }
                catch (Exception ex)
                {
                    pastErro = true;
                    FFWriteLine("未知错误，请检查路径", WriteTy.Warn, ex);
                }
            }

            // 递归搜索文件的方法
            private void SearchFilesRecursively(string path, bool isPack, bool onHash0, List<WBFileInfo> WPFilesList, WBFilesInfo? sWPFileInfo = null)
            {
                try
                {
                    //文件
                    foreach (var file in Directory.GetFiles(path))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);//文件信息对象
                            var wpFileInfo = new WBFileInfo();//水球文件信息对象
                            if (isPack) wpFileInfo = new WBFileInfo2();
                            wpFileInfo.Name = fileInfo.Name;//文件名
                            wpFileInfo.Length = fileInfo.Length;//大小
                            //将当前文件添加到集合
                            WPFilesList.Add(wpFileInfo);
                            //若存在父文件夹对象
                            if (sWPFileInfo != null)
                            {
                                //将本文件的大小附加，即为父文件的大小
                                sWPFileInfo.Length += wpFileInfo.Length;
                                //附加此文件
                                sWPFileInfo.FilesCount++;
                            }
                        }
                        catch (UnauthorizedAccessException uae)
                        {
                            pastErro = true;
                            FFWriteLine("拒绝访问", WriteTy.Warn, uae);
                        }
                        catch (Exception ex)
                        {
                            pastErro = true;
                            FFWriteLine("未知错误", WriteTy.Warn, ex);
                        }
                    }

                    string[] directorys = Directory.GetDirectories(path);
                    //多线程搜索
                    Task[] tasks = new Task[directorys.Length];
                    //文件夹
                    for (int index = 0; index < directorys.Length; index++)
                    {
                        string directory = directorys[index];


                        var directoryInfo = new DirectoryInfo(directory);//文件夹信息对象
                        var wpFilesInfo = new WBFilesInfo//水球文件对象
                        {
                            Name = directoryInfo.Name,//文件名
                            IsD = true//是文件夹
                        };
                        //添加到集合
                        WPFilesList.Add(wpFilesInfo);
                        Task task = new(() =>
                                         {
                                             //更新进度
                                             taskCount++;
                                             //递归搜索子文件
                                             SearchFilesRecursively(directory, isPack, onHash0, wpFilesInfo.FilesList, wpFilesInfo);
                                             //若存在父文件夹对象
                                             if (sWPFileInfo != null)
                                             {
                                                 //将本文件夹的大小附加，即为父文件的大小
                                                 sWPFileInfo.Length += wpFilesInfo.Length;
                                                 //将本文件夹的文件数附加
                                                 sWPFileInfo.FilesCount += wpFilesInfo.FilesCount;
                                             }
                                         });
                        task.Start();
                        tasks[index] = task;
                    }
                    //等待綫程完成
                    for (int index = 0; index < tasks.Length; index++)
                    {
                        try
                        {
                            tasks[index].Wait();//等待
                            tasks[index].Dispose();//释放
                            //更新进度
                            taskCount--;
                        }
                        catch { }
                    }

                }
                catch (UnauthorizedAccessException uae)
                {
                    pastErro = true;
                    FFWriteLine("拒绝访问", WriteTy.Warn, uae);
                }
                catch (Exception ex)
                {
                    pastErro = true;
                    FFWriteLine("未知错误", WriteTy.Warn, ex);
                }
            }
            static void FFWriteLine(string info = "", WriteTy Ty = WriteTy.Info, Exception? ex = null, double Progress = 0)
            {
                WriteLine($"[搜索]{info}", Ty, ex, Progress);
            }

            // 将搜索结果转换为JSON字符串的方法
            public override string ToString()
            {
                return ToString();
            }
            public string ToString(Formatting jsonFo = Formatting.None)
            {
                return JsonConvert.SerializeObject(this, jsonFo);
            }

            //获取Json数据数组
            public byte[] ToJsonBytes(Formatting jsonFo = Formatting.None)
            {
                MemoryStream memoryStream = new();
                StreamWriter streamWriter = new(memoryStream);
                JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = jsonFo });
                jsonSerializer.Serialize(streamWriter, this);
                streamWriter.Flush();
                memoryStream.Flush();
                memoryStream.Position = 0;
                byte[] bytes = new byte[memoryStream.Length];
                memoryStream.Read(bytes);
                return bytes;
            }
            //写入到文件
            public long WriterToFile(FileStream fileStream, Formatting jsonFo = Formatting.None)
            {
                StreamWriter streamWriter = new(fileStream);
                JsonSerializer jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings { Formatting = jsonFo });
                jsonSerializer.Serialize(streamWriter, this);
                streamWriter.Flush();
                fileStream.Flush();
                return fileStream.Length;
            }
        }
    }
}
