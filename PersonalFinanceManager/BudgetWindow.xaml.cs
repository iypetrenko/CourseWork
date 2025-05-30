﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PersonalFinanceManager.Consts;
using PersonalFinanceManager.Model;
using PersonalFinanceManager.Repository;
using PersonalFinanceManager.Repository.Interface;
using Microsoft.Win32;
using OfficeOpenXml;

namespace PersonalFinanceManager
{
    public partial class BudgetWindow : Window
    {
        private readonly IItemRepository _itemList = new ItemRepository();
        private readonly IExpenseCategoryRepository _categoryList = new ExpenseCategoryRepository();
        private List<Category> Categories { get; set; }
        private User _currentUser;
        // Устанавливаем лицензию EPPlus при загрузке класса
        // Updated to use the correct method for setting the license
        static BudgetWindow()
        {
            // Set the EPPlus license to non-commercial using the appropriate method
            ExcelPackage.License.SetNonCommercialPersonal("Your Name or Organization");
        }


        public BudgetWindow(User user)
        {
            _currentUser = user;
            InitializeComponent();

            float pieWidth = 650, pieHeight = 650, centerX = pieWidth / 2, centerY = pieHeight / 2, radius = pieWidth / 2;
            mainCanvas.Width = pieWidth;
            mainCanvas.Height = pieHeight;

            var expenseCategories = _categoryList.GetExpenseCategoriesList()
                .Where(c => c.UserId == SessionInfo.UserId).ToList();

            var items = _itemList.GetUserItems(SessionInfo.UserId);
            Categories = new List<Category>();

            // Исправленный подсчет сумм
            decimal grandTotalSpent = items.Sum(i => i.Price);
            decimal budgetLimit = expenseCategories.Sum(c => c.MonthlyBudget);
            BudgetText.DataContext = new Limits() { Limit = "Виділений бюджет: " + budgetLimit };

            SpentText.DataContext = new Limits() { Limit = "Загальні витрати: " + grandTotalSpent };

            // Перевірка, чи є витрати взагалі
            if (grandTotalSpent > 0)
            {
                foreach (var expenseCategory in expenseCategories)
                {
                    var auxItems = _itemList.GetItems(expenseCategory.Id);
                    decimal categoryTotal = auxItems.Sum(i => i.Price);

                    Categories.Add(new Category
                    {
                        Title = expenseCategory.Name,
                        Percentage = (float)(categoryTotal / grandTotalSpent * 100),
                        ColorBrush = PickBrush()
                    });
                }

                detailsItemsControl.ItemsSource = Categories;

                float angle = 0, prevAngle = 0;
                foreach (var category in Categories)
                {
                    // Пропускаємо категорії з нульовим відсотком - не відображаємо їх на діаграмі
                    if (category.Percentage <= 0)
                        continue;

                    double line1X = (radius * Math.Cos(angle * Math.PI / 180)) + centerX;
                    double line1Y = (radius * Math.Sin(angle * Math.PI / 180)) + centerY;

                    angle = category.Percentage * (float)360 / 100 + prevAngle;
                    Debug.WriteLine(angle);

                    double arcX = (radius * Math.Cos(angle * Math.PI / 180)) + centerX;
                    double arcY = (radius * Math.Sin(angle * Math.PI / 180)) + centerY;

                    var line1Segment = new LineSegment(new Point(line1X, line1Y), false);
                    double arcWidth = radius, arcHeight = radius;
                    bool isLargeArc = category.Percentage > 50;
                    var arcSegment = new ArcSegment()
                    {
                        Size = new Size(arcWidth, arcHeight),
                        Point = new Point(arcX, arcY),
                        SweepDirection = SweepDirection.Clockwise,
                        IsLargeArc = isLargeArc,
                    };
                    var line2Segment = new LineSegment(new Point(centerX, centerY), false);

                    var pathFigure = new PathFigure(
                        new Point(centerX, centerY),
                        new List<PathSegment>()
                        {
                            line1Segment,
                            arcSegment,
                            line2Segment,
                        },
                        true);

                    var pathFigures = new List<PathFigure>() { pathFigure, };
                    var pathGeometry = new PathGeometry(pathFigures);
                    var path = new System.Windows.Shapes.Path()
                    {
                        Fill = category.ColorBrush,
                        Data = pathGeometry,
                    };
                    mainCanvas.Children.Add(path);

                    prevAngle = angle;

                    var outline1 = new Line()
                    {
                        X1 = centerX,
                        Y1 = centerY,
                        X2 = line1Segment.Point.X,
                        Y2 = line1Segment.Point.Y,
                        Stroke = Brushes.White,
                        StrokeThickness = 5,
                    };
                    var outline2 = new Line()
                    {
                        X1 = centerX,
                        Y1 = centerY,
                        X2 = arcSegment.Point.X,
                        Y2 = arcSegment.Point.Y,
                        Stroke = Brushes.White,
                        StrokeThickness = 5,
                    };

                    mainCanvas.Children.Add(outline1);
                    mainCanvas.Children.Add(outline2);
                }
            }
            else
            {
                // Відображення порожньої діаграми або повідомлення, якщо немає витрат
                foreach (var expenseCategory in expenseCategories)
                {
                    Category aux = new Category();
                    aux.Title = expenseCategory.Name;
                    aux.Percentage = 0;
                    aux.ColorBrush = PickBrush();
                    Categories.Add(aux);
                }

                detailsItemsControl.ItemsSource = Categories;

                // Можна додати текстове повідомлення на діаграму
                TextBlock noDataText = new TextBlock
                {
                    Text = "Немає даних для відображення",
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Canvas.SetLeft(noDataText, centerX - 150);
                Canvas.SetTop(noDataText, centerY - 12);
                mainCanvas.Children.Add(noDataText);
            }
        }

        private void NavigateToMainPage()
        {
            var mainWindow = new MainWindow(_currentUser);
            mainWindow.Show();
            Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToMainPage();
        }

        public Brush PickBrush()
        {
            var r = new Random();
            var properties = typeof(Brushes).GetProperties();
            var count = properties.Count();
            var colour = properties
            .Select(x => new { Property = x, Index = r.Next(count) })
            .OrderBy(x => x.Index)
            .First();
            return (SolidColorBrush)colour.Property.GetValue(colour, null);
        }

        private List<SpendingCategories> GetUserExpenses()
        {
            var expenseCategories = _categoryList.GetExpenseCategoriesList()
                .Where(c => c.UserId == SessionInfo.UserId).ToList();

            var items = _itemList.GetUserItems(SessionInfo.UserId);

            List<SpendingCategories> output = new List<SpendingCategories>();

            foreach (var cat in expenseCategories)
            {
                var aux = new SpendingCategories
                {
                    Name = cat.Name,
                    MonthlyBudget = cat.MonthlyBudget,
                    Expenses = items.Where(i => i.ToDoListId == cat.Id).Sum(i => i.Price)
                };

                output.Add(aux);
            }

            return output;
        }

        private async Task SaveExcelFile(List<SpendingCategories> myCategories, FileInfo file)
        {
            try
            {
                if (file.Exists)
                {
                    file.Delete();
                }

                using (var package = new ExcelPackage(file))
                {
                    var ws = package.Workbook.Worksheets.Add("BudgetReport");

                    var range = ws.Cells["A1"].LoadFromCollection(myCategories, true);
                    range.AutoFitColumns();

                    await package.SaveAsync();
                }

                MessageBox.Show("Файл успешно сохранен на рабочем столе: " + file.FullName,
                    "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении файла: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Export_ClickAsync(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var myCategories = GetUserExpenses();

                // Создаем объект FileInfo
                var file = new FileInfo(System.IO.Path.Combine(path, "BudgetData.xlsx"));

                // Добавляем await перед вызовом асинхронного метода
                await SaveExcelFile(myCategories, file);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Произошла ошибка при экспорте: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class Category
    {
        public float Percentage { get; set; }
        public string Title { get; set; }
        public Brush ColorBrush { get; set; }
    }

    public class Limits
    {
        public string Limit { get; set; }
    }

    public class SpendingCategories
    {
        public string Name { get; set; }
        public decimal MonthlyBudget { get; set; }
        public decimal Expenses { get; set; }
    }
}