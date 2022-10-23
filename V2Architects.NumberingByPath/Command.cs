using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace V2Architects.NumberingByPath
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    class Command : IExternalCommand
    {
        private UIApplication _uiApp;
        private Document _doc;
        private MainWindow _mainWindow;
        private CurveElement _detailNurbSpline;
        private int _count = 0;
        private string _mes = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            _uiApp = commandData.Application;
            _doc = commandData.Application.ActiveUIDocument.Document;

            _mainWindow = new MainWindow();
            _mainWindow.DataContext = this;

            try
            {
                using (var t = new Transaction(_doc, "Нумерация по сплайну"))
                {
                    t.Start();

                    _mainWindow.ShowDialog();

                    t.Commit();
                }

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                TaskDialog.Show("Отмена", "Операция отменена пользователем.");
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Ошибка", message);
                return Result.Failed;
            }
        }

        private void SetRoomsNumber()
        {
            SelectSpline();
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfCategory(BuiltInCategory.OST_Rooms);

            NurbSpline nurbSpline = _detailNurbSpline.GeometryCurve as NurbSpline;
            IList<XYZ> points = nurbSpline.CtrlPoints;
            List<Room> rooms = points.Select(x => GetRoomOfGroup(x)).ToList();
            rooms = rooms.Where(x => x != null).ToList();
            _count = rooms.Count;

            int startNumbet = Convert.ToInt32(FirstNumber); int i = 0;

            List<Room> rooms1 = rooms.Distinct(new SortYearAscendingHelper()).ToList();
            rooms1.ForEach(x => x.LookupParameter("Номер").Set(GetRoomNumber(startNumbet + i++)));

            _count = rooms1.Count();


            ShowReport();
        }

        private string GetRoomNumber(int i)
        {
            int roomstartnumberlen = i.ToString().Length;

            //string mask = FirstNumber.Remove(FirstNumber.Length - i.ToString().Length, i.ToString().Length);

            return FirstNumber.Length > i.ToString().Length ? $"{Prefix}{FirstNumber.Remove(FirstNumber.Length - i.ToString().Length, i.ToString().Length)}{i}" : $"{Prefix}{i.ToString()}";
        }

        private class SortYearAscendingHelper : IEqualityComparer<Room>
        {
            public bool Equals(Room x, Room y)
            {
               return x.Id == y.Id;
            }

            public int GetHashCode(Room obj)
            {
                //int hCode = Convert.ToInt32(obj.Number);
                return 0;
            }
        }


        /// Возвращает комнату, в которой находится точка
        private Room GetRoomOfGroup(XYZ point)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfCategory(BuiltInCategory.OST_Rooms);

            Room room = null;

            foreach (Element elem in collector)
            {
                room = elem as Room;
                if (room != null)
                {
                    // Точка в указанной комнате?
                    if (room.IsPointInRoom(point))
                    {
                        return room;
                    }
                }
            }

            return null;
        }

        private string _Prefix { get; set; } = "00";

        /// <summary>
        /// заполнение префикса
        /// </summary>
        public string Prefix
        {
            get => _Prefix;
            set
            {
                _Prefix = value;
                //OnPropertyChanged();
            }
        }

        private string _FirstNumber { get; set; } = "1";

        /// <summary>
        /// заполнение префикса
        /// </summary>
        public string FirstNumber
        {
            get => _FirstNumber;
            set
            {
                _FirstNumber = value;
                //OnPropertyChanged();
            }
        }

        private RelayCommand _Btn;

        /// <summary>
        /// Команда запуска определения помещения 
        /// </summary>
        public RelayCommand Btn
        {
            get
            {
                return _Btn ??
                    (_Btn = new RelayCommand(obj => { _mainWindow.Close(); SetRoomsNumber(); }));
            }
        }

        private void SelectSpline()
        {
            //Выбор объекта
            Selection sel = _uiApp.ActiveUIDocument.Selection;
            Reference pickedRef = null;

            //SplinePickFilter selFilter = new SplinePickFilter();

            //pickedRef = sel.PickObject(ObjectType.Element, selFilter, "Выбери сплайн");
            pickedRef = sel.PickObject(ObjectType.Element, "Выбери сплайн");
            var t = _doc.GetElement(pickedRef);
            _detailNurbSpline = _doc.GetElement(pickedRef) as DetailNurbSpline;
            if (_detailNurbSpline == null)
            {
                _detailNurbSpline = _doc.GetElement(pickedRef) as ModelNurbSpline;
            }
        }

        //public class SplinePickFilter : ISelectionFilter
        //{
        //    public bool AllowElement(Element e)
        //    {
        //        if (e == null) return false;
        //        return (e.Category.Id.IntegerValue.Equals(
        //        (int)BuiltInCategory.OST_Lines));
        //    }
        //    public bool AllowReference(Reference r, XYZ p)
        //    {
        //        return false;
        //    }
        //}

        private void ShowReport()
        {
            var reportWindow = new ReportWindow($"Обновлено {_count}");
            reportWindow.Show();
        }

        public class RelayCommand : ICommand
        {
            private Action<object> execute;
            private Func<object, bool> canExecute;

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                this.execute = execute;
                this.canExecute = canExecute;
            }

            public bool CanExecute(object parameter)
            {
                return this.canExecute == null || this.canExecute(parameter);
            }

            public void Execute(object parameter)
            {
                this.execute(parameter);
            }
        }
    }
}
