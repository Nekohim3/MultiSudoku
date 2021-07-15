using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using CommandLine;

namespace MultiSudoku
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Путь к файлу с входными данными")]
        public string DataFileName { get; set; }
        [Option('t', "template", Required = true, HelpText = "Путь к файлу с шаблоном")]
        public string TemplateFileName { get; set; }
        [Option('o', "onefile", Required = false, HelpText = "")]
        public bool OneFileOutput { get; set; }
        [Option('a', "all", Required = false, HelpText = "")]
        public bool IgnoreTask { get; set; }
    }

    public static class g
    {
        public static int qqq { get; set; }
        public static int i { get; set; }
    }
    class Program
    {
        private static int      _threadCount = 16;

        private static Thread[]                            _loadDataThreadArray;
        private static bool[]                              _loadDataFinishArray;
        private static object                              _loadDataLock;
        private static bool                                _loadDataWork;
        private static ConcurrentQueue<Tuple<int, string>> _tsDataList;

        private static Thread[] _chainAnalyzeThreadArray;
        private static bool[]   _chainAnalyzeFinishArray;
        private static object   _chainAnalyzeLock;


        private static Thread[] _generateMultiSudokuThreadArray;
        private static bool[]   _generateMultiSudokuFinishArray;
        private static object   _generateMultiSudokuLock;
        private static int[]   _generateMultiSudokuLastId;

        private static List<SudokuData> _sudokuDataList;
        private static List<int[]>[]    _chain;
        private static List<int[]>      _multiSudokuList;

        private static TemplateTreeSudokuNode          _templateTreeSudokuRoot;
        private static List<TemplateFieldSudokuSquare> _templateFieldSudokuSquareList;

        private static List<int> _indexIdList = new List<int>();


        private static void ApplicationOnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            if ((e.Exception).Message.Contains("OutOfMemoryException"))
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка OutOfMemoryException");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка");
                Console.WriteLine();
            }
            Logger.Log.Error($"ApplicationOnThreadException -> "         +
                             $"{(e.Exception).Message} -> " +
                             $"{(e.Exception).Source} -> "  +
                             $"{(e.Exception).StackTrace}");
            
            Process.GetCurrentProcess().Kill();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (((Exception) e.ExceptionObject).Message.Contains("OutOfMemoryException"))
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка OutOfMemoryException");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Ошибка");
                Console.WriteLine();
            }
            Logger.Log.Error($"CurrentDomain_UnhandledException -> "         +
                             $"{((Exception)e.ExceptionObject).Message} -> " +
                             $"{((Exception)e.ExceptionObject).Source} -> "  +
                             $"{((Exception)e.ExceptionObject).StackTrace}");
            
            Process.GetCurrentProcess().Kill();
        }

        private static void Main(string[] args)
        {
            var p = new Parser(config => config.HelpWriter = null);
            p.ParseArguments<Options>(args)
                       .WithParsed(RunOptions)
                       .WithNotParsed(HandleParseError);
            
        }

        static void RunOptions(Options opts)
        {
            Console.WriteLine();
            Logger.Init();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException                += ApplicationOnThreadException;
            InitSudokuFieldTemplate(opts.TemplateFileName);
            InitSudokuTreeTemplate();
            LoadAndInitSudokuData(opts.DataFileName);
            ChainAnalyze(opts.IgnoreTask);
            PostInitProcessing();
            if(opts.IgnoreTask)
                GenerateMultiSudokusLight();
            else
                GenerateMultiSudokus();
            WriteMultisudoku(opts.OneFileOutput);
            Console.WriteLine();
        }
        static void HandleParseError(IEnumerable<Error> errs)
        {
            Console.WriteLine("ОШИБКА:");

            foreach (MissingRequiredOptionError error in errs)
                Console.WriteLine($"\tПропущен обязательный параметр: {error.NameInfo.NameText}");

            PrintHelp();
        }

        static void PrintHelp()
        {
            Console.WriteLine("");
            Console.WriteLine("Справка:");
            Console.WriteLine($"\t-t, --template (обязательный) - Путь к файлу с шаблоном");
            Console.WriteLine($"\t-i, --input (обязательный) - Путь к файлу с данными");
            Console.WriteLine($"\t-o, --onefile (необязательный) - Выводить найденные мультисудоку в один файл");
            Console.WriteLine($"\t-a, --all (необязательный) - Игнорировать задания при вычислении подходящих мультисудоку");
        }

        private static void InitSudokuFieldTemplate(string fileName)
        {
            Console.Write("Загрузка шаблона...");
            Logger.Log.Information("InitSudokuFieldTemplate");
            _templateFieldSudokuSquareList = new List<TemplateFieldSudokuSquare>();
            var lines = new List<string>();
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
                while (!sr.EndOfStream)
                    lines.Add(sr.ReadLine());

            for (var i = 1; i < lines.Count; i += 2)
            for (var j = 1; j < lines[i].Length; j += 2)
                if (lines[i][j] == 'O')
                    _templateFieldSudokuSquareList.Add(new TemplateFieldSudokuSquare() {Position = new Point(j, i), Id = _templateFieldSudokuSquareList.Count});
        }

        private static void InitSudokuTreeTemplate()
        {
            Logger.Log.Information("InitSudokuTreeTemplate");
            _templateTreeSudokuRoot = new TemplateTreeSudokuNode() { DirectionFromParent = -1, TemplateFieldSudoku = _templateFieldSudokuSquareList[0], Index = _indexIdList.Count};
            var usedIds = new List<int>();
            _indexIdList.Add(_templateFieldSudokuSquareList[0].Id);
            CreateSudokuTreeTemplate(_templateTreeSudokuRoot, usedIds);
            Console.WriteLine("OK");
        }

        private static void CreateSudokuTreeTemplate(TemplateTreeSudokuNode node, List<int> usedIds)
        {
            usedIds.Add(node.TemplateFieldSudoku.Id);

            for (var i = 0; i < 4; i++)
            {
                var nextPoint                     = node.TemplateFieldSudoku.GetNextPositionByDirection(i);
                var nextTemplateSudokuFieldSquare = _templateFieldSudokuSquareList.FirstOrDefault(x => x.Position == nextPoint);

                if (nextTemplateSudokuFieldSquare == null || usedIds.Contains(nextTemplateSudokuFieldSquare.Id)) continue;

                node.Childs.Add(new TemplateTreeSudokuNode(){DirectionFromParent = i, TemplateFieldSudoku = nextTemplateSudokuFieldSquare, Index = _indexIdList.Count });

                _indexIdList.Add(node.Childs.Last().TemplateFieldSudoku.Id);
                CreateSudokuTreeTemplate(node.Childs.Last(), usedIds);
            }
        }
        
        private static void LoadAndInitSudokuData(string fileName)
        {
            Console.Write("Загрузка данных из файла...");
            Logger.Log.Information($"LoadAndInitSudokuData {fileName}");
            _sudokuDataList      = new List<SudokuData>();
            _loadDataWork        = true;
            _loadDataThreadArray = new Thread[_threadCount];
            _loadDataFinishArray = new bool[_threadCount];
            _tsDataList          = new ConcurrentQueue<Tuple<int, string>>();
            _loadDataLock        = new object();

            for (var i = 0; i < _threadCount; i++)
            {
                var ii = i;
                _loadDataThreadArray[ii] = new Thread(() => { LoadAndInitSudokuDataThreadFunction(ii); });
                _loadDataThreadArray[ii].Start();
            }

            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            using (var sr = new StreamReader(fs))
            {
                var id = 0;

                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    _tsDataList.Enqueue(Tuple.Create(id, line));
                    id++;
                }
            }


            while (_tsDataList.Count != 0)
                Thread.Sleep(10);

            _loadDataWork = false;

            while (_loadDataFinishArray.Any(x => x == false))
                Thread.Sleep(10);

            GC.Collect();
            Console.WriteLine($"OK. Найдено {_sudokuDataList.Count} судоку");
            Logger.Log.Information($"LoadAndInitSudokuData finish. Founded sudoku data: {_sudokuDataList.Count}");

        }

        private static void LoadAndInitSudokuDataThreadFunction(int id)
        {
            while (_loadDataWork)
            {
                while (_tsDataList.TryDequeue(out var res))
                {
                    var sudokuData = new SudokuData(res.Item1, res.Item2);

                    lock (_loadDataLock)
                    {
                        _sudokuDataList.Add(sudokuData);
                    }
                }
                Thread.Sleep(1);
            }

            _loadDataFinishArray[id] = true;
        }

        private static void ChainAnalyze(bool ignoreTask)
        {
            Console.Write("Обработка данных...");
            Logger.Log.Information($"ChainAnalyze");
            _chain                   = new List<int[]>[4];
            _chain[0]                = new List<int[]>();
            _chain[1]                = new List<int[]>();
            _chain[2]                = new List<int[]>();
            _chain[3]                = new List<int[]>();
            _chainAnalyzeThreadArray = new Thread[_threadCount];
            _chainAnalyzeFinishArray = new bool[_threadCount];
            _chainAnalyzeLock        = new object();

            for (var i = 0; i < 2; i++)
            {
                var ii = i;
                Utils.RadixSort.SudokuDataSortByPoint(_sudokuDataList, Utils.Directions.GetInvDirectionPoint(ii));

                for (var j = 0; j < _threadCount; j++)
                {
                    var jj = j;
                    _chainAnalyzeFinishArray[jj] = false;
                    _chainAnalyzeThreadArray[jj] = new Thread(() => { ChainAnalyzeThreadFunction(jj, Utils.Directions.GetDirectionPoint(ii), Utils.Directions.GetInvDirectionPoint(ii), ii, ignoreTask); });
                    _chainAnalyzeThreadArray[jj].Start();
                }

                while (_chainAnalyzeFinishArray.Any(x => x == false))
                    Thread.Sleep(10);

                GC.Collect();
            }
            
            foreach (var c in _chain[0])
                _chain[2].Add(new[] { c[1], c[0] });
            foreach (var c in _chain[1])
                _chain[3].Add(new[] { c[1], c[0] });

            Logger.Log.Information($"ChainAnalyze finish. Founded chains: {_chain[0].Count} {_chain[1].Count} {_chain[2].Count} {_chain[3].Count}");
        }

        private static void ChainAnalyzeThreadFunction(int offset, Point from, Point to, int directrion, bool ignoreTask) => ChainAnalyzeThreadFunction(offset, from.X, from.Y, to.X, to.Y, directrion, ignoreTask);

        private static void ChainAnalyzeThreadFunction(int offset, int fx, int fy, int tx, int ty, int directrion, bool ignoreTask)
        {
            for (var i = offset; i < _sudokuDataList.Count; i += _threadCount)
            {
                var indexList = Utils.BinarySearch.SearchSudokuDataRange(_sudokuDataList, _sudokuDataList[i], fx, fy, tx, ty, ignoreTask);

                foreach (var index in indexList)
                    lock(_chainAnalyzeLock)
                        _chain[directrion].Add(new[] {_sudokuDataList[i].Id, _sudokuDataList[index].Id});
            }

            _chainAnalyzeFinishArray[offset] = true;
        }

        private static void PostInitProcessing()
        {
            Logger.Log.Information($"PostInitProcessing");
            Utils.RadixSort.SudokuRadixSortById(_sudokuDataList);

            for (var i = 0; i < _chain.Length; i++)
            {
                Utils.RadixSort.ChainRadixSortBy(_chain, i, 1);
                Utils.RadixSort.ChainRadixSortBy(_chain, i, 0);
            }

            GC.Collect();
            Console.WriteLine($"OK");
            Logger.Log.Information($"Init done");
        }
        
        private static void GenerateMultiSudokusLight()
        {
            Console.Write("Генерация мультисудоку*...");
            Logger.Log.Information($"GenerateMultiSudokusLigth");
            _multiSudokuList                = new List<int[]>();
            _generateMultiSudokuLock        = new object();
            _generateMultiSudokuFinishArray = new bool[_threadCount];
            _generateMultiSudokuThreadArray = new Thread[_threadCount];
            _generateMultiSudokuLastId      = new int[_threadCount];

            for (var i = 0; i < _threadCount; i++)
            {
                var ii = i;
                _generateMultiSudokuThreadArray[ii] = new Thread(() => { GenerateMultiSudokusLightThread(ii); });
                _generateMultiSudokuThreadArray[ii].Start();
            }

            while (_generateMultiSudokuFinishArray.Any(x => x == false))
            {
                Thread.Sleep(100);
                var pc = 100 * (float) _generateMultiSudokuLastId.Average() / (float) _chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent].Count;
                Console.Write($"\rГенерация мультисудоку*... ({pc:F}% Найдено мультисудоку: {_multiSudokuList.Count})                    ");
            }

            Console.WriteLine($"\rГенерация мультисудоку*... (100% Найдено мультисудоку: {_multiSudokuList.Count})                    ");
        }

        public static void GenerateMultiSudokusLightThread(int id)
        {
            for (var i = id; i < _chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent].Count; i += _threadCount)
            {
                if (_generateMultiSudokuLastId[id] < i)
                    _generateMultiSudokuLastId[id] = i;
                if (_sudokuDataList[_chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent][i][0]].Used) continue;
                var msArr = new int[_templateFieldSudokuSquareList.Count];
                msArr.Fill(-1);
                msArr[0] = _chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent][i][0];
                var succGen = GenerateMultiSudokusLight(_templateTreeSudokuRoot, ref msArr);

                if (!succGen) continue;

                foreach (var t in msArr)
                    _sudokuDataList[t].Used = true;

                lock(_generateMultiSudokuLock)
                    _multiSudokuList.Add(msArr);

            }

            _generateMultiSudokuFinishArray[id] = true;
        }


        private static bool GenerateMultiSudokusLight(TemplateTreeSudokuNode node, ref int[] temp)
        {
            var done = temp.All(i => i != -1);

            if(done)
                if (!CheckTempList1(temp))
                    done = false;

            if (done) return true;

            if (node.Childs.Count == 0) return false;

            foreach (var child in node.Childs)
            {
                if (temp[node.TemplateFieldSudoku.Id] == -1) return false;

                var range = Utils.BinarySearch.SearchChainRange(_chain, temp[node.TemplateFieldSudoku.Id], child.DirectionFromParent);

                if (range.Item1 == -1 || range.Item2 == -1) continue;

                for (var j = range.Item1; j <= range.Item2; j++)
                {
                    if (!temp.Contains(_chain[child.DirectionFromParent][j][1]) && !_sudokuDataList[_chain[child.DirectionFromParent][j][1]].Used)
                    {
                        temp[child.TemplateFieldSudoku.Id] = _chain[child.DirectionFromParent][j][1];
                        var succGen = GenerateMultiSudokusLight(child, ref temp);

                        if (succGen) return true;
                        else
                        {
                            var ind = int.MaxValue;

                            for (var ii = 0; ii < _indexIdList.Count - 1 && ind == int.MaxValue; ii++)
                            {
                                if (temp[_indexIdList[ii]] != -1 && temp[_indexIdList[ii + 1]] == -1)
                                    ind = ii;
                            }
                            if(child.Index >= ind + 1)
                            {
                                temp[child.TemplateFieldSudoku.Id] = -1;
                            }

                        }
                    }
                }

                if (temp[child.TemplateFieldSudoku.Id] == -1) return false;

            }

            return false;
        }

        private static void GenerateMultiSudokus()
        {
            Console.Write("Генерация мультисудоку...");
            Logger.Log.Information($"GenerateMultiSudokus");
            _multiSudokuList = new List<int[]>();
            var tempList = new List<int[]>();
            for (var i = 0; i < _chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent].Count; i++)
            {
                g.i = i;
                if(_sudokuDataList[_chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent][i][0]].Used) continue;

                var msArr = new int[_templateFieldSudokuSquareList.Count];
                msArr.Fill(-1);
                msArr[0] = _chain[_templateTreeSudokuRoot.Childs[0].DirectionFromParent][i][0];
                tempList.Add(msArr);
                GenerateMultiSudokus(_templateTreeSudokuRoot, ref tempList);
                CheckTempList(tempList);
                _multiSudokuList.AddRange(tempList.ToList());
                tempList.Clear();
            }
            Logger.Log.Information($"GenerateMultiSudokus finish {_multiSudokuList.Count}");
            Console.WriteLine($"OK. Найдено {_multiSudokuList.Count} мультисудоку");
        }

        private static bool CheckTempList1(int[] temp)
        {
            var qwe = true;

            for (var i = 0; i < _templateFieldSudokuSquareList.Count && qwe; i++)
            for (var j = 0; j < 4 && qwe; j++)
            {
                var nextSudoku = _templateFieldSudokuSquareList.FirstOrDefault(x => x.Position == _templateFieldSudokuSquareList[i].GetNextPositionByDirection(j));

                if (nextSudoku == null) continue;

                var rect = Utils.Directions.GetChainRect(j);

                if (!_sudokuDataList[temp[i]].IsEqual(_sudokuDataList[temp[nextSudoku.Id]], rect.X, rect.Y, rect.Width, rect.Height))
                    qwe = false;
            }

            return qwe;
        }

        private static void CheckTempList(List<int[]> tempList)
        {
            for (var i = 0; i < tempList.Count; i++)
                if (tempList[i].Contains(-1))
                    tempList.RemoveAt(i--);

            for (var k = 0; k < tempList.Count; k++)
            {
                var qwe = true;

                for (var i = 0; i < _templateFieldSudokuSquareList.Count && qwe; i++)
                for (var j = 0; j < 4 && qwe; j++)
                {
                    var nextSudoku = _templateFieldSudokuSquareList.FirstOrDefault(x => x.Position == _templateFieldSudokuSquareList[i].GetNextPositionByDirection(j));

                    if (nextSudoku == null) continue;

                    var rect = Utils.Directions.GetChainRect(j);

                    if (!_sudokuDataList[tempList[k][i]].IsEqual(_sudokuDataList[tempList[k][nextSudoku.Id]], rect.X, rect.Y, rect.Width, rect.Height))
                        qwe = false;
                }

                if (!qwe)
                    tempList.RemoveAt(k--);
            }

            for (var i = 0; i < tempList.Count; i++)
            {
                var b = true;

                for (var j = 0; j < tempList[i].Length && b; j++)
                    if (_sudokuDataList[tempList[i][j]].Used)
                        b = false;

                if (!b)
                    tempList.RemoveAt(i--);
                else
                    for (var j = 0; j < tempList[i].Length && b; j++)
                        _sudokuDataList[tempList[i][j]].Used = true;
            }

        }

        private static void GenerateMultiSudokus(TemplateTreeSudokuNode node, ref List<int[]> tempList)
        {
            if(node.Childs.Count == 0) return;

            var list = new List<int[]>();

            foreach (var child in node.Childs)
            {
                for (var i = 0; i < tempList.Count; i++)
                {
                    var range = Utils.BinarySearch.SearchChainRange(_chain, tempList[i][node.TemplateFieldSudoku.Id], child.DirectionFromParent);

                    if(range.Item1 == -1 || range.Item2 == -1) continue;

                    for (var j = range.Item1; j <= range.Item2; j++)
                    {
                        if (!tempList[i].Contains(_chain[child.DirectionFromParent][j][1]) && !_sudokuDataList[_chain[child.DirectionFromParent][j][1]].Used)
                        {
                            list.Add(tempList[i].ExtendCopy(child.TemplateFieldSudoku.Id, _chain[child.DirectionFromParent][j][1]));
                        }
                    }
                }

                tempList = list.ToList();
                list.Clear();
            }

            foreach (var child in node.Childs)
            {
                GenerateMultiSudokus(child, ref tempList);
            }
        }

        private static void WriteMultisudoku(bool singleFileOutput)
        {
            if (!Directory.Exists("Output"))
                Directory.CreateDirectory("Output");
            if(singleFileOutput)
            {
                Console.Write("Запись мультисудоку в файл...");
                using (var fs = new FileStream("Output/1.txt", FileMode.Create, FileAccess.Write))
                using (var sw = new StreamWriter(fs))
                {
                    for (var i = 0; i < _multiSudokuList.Count; i++)
                    {
                        for (var j = 0; j < _multiSudokuList[i].Length; j++)
                            sw.WriteLine(_sudokuDataList[_multiSudokuList[i][j]]);
                        if(i < _multiSudokuList.Count - 1)
                            sw.WriteLine("//");
                    }
                }
            }
            else
            {
                Console.Write("Запись мультисудоку в файлы...");
                for (var i = 0; i < _multiSudokuList.Count; i++)
                {
                    var str = "";

                    for (var j = 0; j < _multiSudokuList[i].Length; j++)
                        str += $"{_sudokuDataList[_multiSudokuList[i][j]]} {(j == _multiSudokuList[i].Length - 1 ? "" : "\r\n")}";

                    File.WriteAllText($"Output/{i}.txt", str);
                }
            }
            Console.Write("OK");
        }
    }
}
