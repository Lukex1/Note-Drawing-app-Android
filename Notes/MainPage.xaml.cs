using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Maui.ColorPicker;
using Microsoft.Maui.Layouts;
using SkiaSharp.Views.Maui;
using SkiaSharp;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Notes
{
    public class TextObject
    {
        public string Text { get; set; }
        public SKPoint Position { get; set; }
        public SKPaint Paint { get; set; }
        public bool isSelected { get; set; }
        public float FontSize { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        private bool isDrawing = false;  // Czy użytkownik rysuje
        private SKPath currentPath;      // Bieżąca ścieżka
        private SKPaint currentPaint;    // Bieżący pędzel
        private List<SKPath> allPaths = new List<SKPath>();   // Lista wszystkich ścieżek
        private List<SKPaint> allPaints = new List<SKPaint>(); // Lista wszystkich pędzli (do zapisywania koloru, opacity itd.)
        private Stack<SKPath> undoStack = new Stack<SKPath>(); // Stos do Undo
        private Stack<SKPath> redoStack = new Stack<SKPath>(); // Stos do Redo
        private Stack<SKPaint> undoPaintStack = new Stack<SKPaint>(); // Stos do przechowywania pędzli dla Undo
        private Stack<SKPaint> redoPaintStack = new Stack<SKPaint>(); // Stos dla pędzli do Redo
        private float Thickness = 1;
        private double Przezroczystosc = 255;
        private bool isScrolling = false;
        private SKPoint scrollOffset = SKPoint.Empty;

        private List<TextObject> TextObjects = new List<TextObject>();
        private TextObject currentTextObject = new TextObject();
        private SKPoint selectedTextPosition = new SKPoint(100, 100);

        private Color kolor;

        byte Red =0;
        byte Green =0;
        byte blue = 0;

        private float FontSize = 30;
        private Stack<TextObject> undoTextStack = new Stack<TextObject>(); // Stack for undoing text actions
        private Stack<TextObject> redoTextStack = new Stack<TextObject>(); // Stack for redoing text actions
        public MainPage()
        {
            InitializeComponent();
            CreateGrid();

            // Ustawienia początkowe malowania
            currentPaint = new SKPaint
            {
                Color = SKColors.Black, // default color
                StrokeWidth = 5,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            SKPaint normalcolor = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = FontSize
            };
            currentPath = new SKPath(); // Inicjalizacja nowej ścieżki
            ScrollContainer.Scrolled += OnScrolled;
            if (currentTextObject.Text==null)
            {
                currentTextObject = new TextObject
                {
                    Text = "",
                    Position = new SKPoint(100, 100),
                    Paint = normalcolor,
                    isSelected = false,
                };
            }

            this.kolor = new Color(0,0,0);
        }

        private void OnScrolled(object sender, ScrolledEventArgs e)
        {
            scrollOffset = new SKPoint((float)e.ScrollX, (float)e.ScrollY);
            CanvasView.InvalidateSurface();
        }
        private void CreateGrid()
        {
            int rows = 50;
            int cols = 50;
            double cellSize = 40;

            DrawingGrid.RowDefinitions.Clear();
            DrawingGrid.ColumnDefinitions.Clear();
            DrawingGrid.Children.Clear();

            // Tworzenie wierszy i kolumn
            for (int i = 0; i < rows; i++)
                DrawingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });

            for (int i = 0; i < cols; i++)
                DrawingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });

            // Dodaj linie poziome
            for (int row = 0; row < rows + 1; row++)
            {
                var line = new BoxView
                {
                    BackgroundColor = Colors.LightGray,
                    HeightRequest = 1,
                    HorizontalOptions = LayoutOptions.FillAndExpand
                };
                DrawingGrid.Children.Add(line);
                Grid.SetRow(line, row);
                Grid.SetColumnSpan(line, cols);
            }

            // Dodaj linie pionowe
            for (int col = 0; col < cols + 1; col++)
            {
                var line = new BoxView
                {
                    BackgroundColor = Colors.LightGray,
                    WidthRequest = 1,
                    VerticalOptions = LayoutOptions.FillAndExpand
                };
                DrawingGrid.Children.Add(line);
                Grid.SetColumn(line, col);
                Grid.SetRowSpan(line, rows);
            }

            DrawingGrid.WidthRequest = cols * cellSize;
            DrawingGrid.HeightRequest = rows * cellSize;
        }

        private void OnToolsClicked(object sender, EventArgs e)
        {
            ToolsFrame.IsVisible = !ToolsFrame.IsVisible;
        }

        private void OnPaintClicked(object sender, EventArgs e)
        {
            Paint.IsVisible = !Paint.IsVisible;
        }

        private void ThicknessSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            ThicknessLabel.Text = $"Grubość: {e.NewValue:F0}";
            this.Thickness = (float)e.NewValue;
            currentPaint.StrokeWidth = (float)e.NewValue; // Zmiana grubości w czasie rzeczywistym
        }

        private void OpacitySlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            OpacityLabel.Text = $"Przezroczystość: {e.NewValue:F0}%";
            this.Przezroczystosc = e.NewValue;
            UpdateCurrentPaint();  // Update the current paint with new opacity
        }

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            canvas.Save();
            canvas.Translate(-scrollOffset.X, -scrollOffset.Y);

            for (int i = 0; i < allPaths.Count; i++)
            {
                var path = allPaths[i];
                var paint = allPaints[i];
                canvas.DrawPath(path, paint);
            }

            // Rysowanie bieżącej ścieżki, jeśli istnieje
            if (currentPath != null)
            {
                canvas.DrawPath(currentPath, currentPaint);
            }

            foreach(var textObject in TextObjects)
            {
                canvas.DrawText(textObject.Text, textObject.Position.X, textObject.Position.Y, textObject.Paint);
                if (textObject.isSelected)
                {
                    float textWidth = textObject.Paint.MeasureText(textObject.Text);
                    float textHeight = textObject.Paint.TextSize;

                    SKRect bounds = new SKRect(textObject.Position.X, textObject.Position.Y - textHeight, textObject.Position.X + textWidth, textObject.Position.Y);

                    SKPaint circlePaint = new SKPaint
                    {
                        Color = SKColors.Red.WithAlpha(128),
                        IsAntialias = true,
                    };
                    canvas.DrawCircle(bounds.MidX,bounds.MidY,20,circlePaint);
                        TextEditOptions.IsVisible = true;
                }
                else {
                    TextEditOptions.IsVisible = false;
                }
                
            }

            canvas.Restore();
        }

        private void OnCanvasTouch(object sender, SKTouchEventArgs e)
        {
            // Calculate global coordinates relative to the virtual canvas
            var globalLocation = new SKPoint(
                e.Location.X + scrollOffset.X,
                e.Location.Y + scrollOffset.Y
            );

            if (isScrolling)
            {
                e.Handled = false; // Allow ScrollView to handle scrolling
                return;
            }

            bool touchedText = false;
            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    foreach (var textObject in TextObjects)
                    {
                        float textWidth = textObject.Paint.MeasureText(textObject.Text);
                        float textHeight = textObject.Paint.TextSize;

                        SKRect bounds = new SKRect(
                            textObject.Position.X,
                            textObject.Position.Y - textHeight,
                            textObject.Position.X + textWidth,
                            textObject.Position.Y
                        );

                        if (bounds.Contains(globalLocation))
                        {
                            // Text is touched, mark it as selected and update its position
                            // Unselect any previously selected text objects
                            foreach (var obj in TextObjects)
                            {
                                obj.isSelected = false;
                            }

                            textObject.isSelected = true;
                            currentTextObject = textObject;
                            currentTextObject.Position = globalLocation;
                            touchedText = true;
                            break;
                        }
                    }

                    if (!touchedText)
                    {
                        // If no text was touched, start drawing a path
                        UpdateCurrentPaint();
                        currentPath = new SKPath();
                        currentPath.MoveTo(globalLocation);
                    }
                    break;

                case SKTouchAction.Moved:
                    if (currentTextObject != null && currentTextObject.isSelected)
                    {
                        // Update text position based on current touch location
                        currentTextObject.Position = globalLocation;
                        // Invalidate surface to trigger redrawing
                        CanvasView.InvalidateSurface();
                    }
                    else if (currentPath != null)
                    {
                        // If we're not moving text, continue drawing the path
                        currentPath.LineTo(globalLocation);
                        CanvasView.InvalidateSurface(); // Refresh the canvas to update the path
                    }
                    break;

                case SKTouchAction.Released:
                    if (currentTextObject != null && currentTextObject.isSelected)
                    {
                        undoTextStack.Push(currentTextObject);
                        SKPaint sett = new SKPaint
                        {
                            Color=new SKColor((byte)(this.kolor.Red*255), (byte)(this.kolor.Green * 255), (byte)(this.kolor.Blue * 255)),
                            TextSize= this.FontSize
                        };
                        currentTextObject.isSelected = false;
                        currentTextObject = new TextObject()
                        {
                            Text = "",
                            Position = new SKPoint(100, 100),
                            Paint = sett,
                            isSelected = false,
                        };
                    }

                    if (currentPath != null)
                    {
                        // After releasing the touch, save the drawn path
                        allPaths.Add(currentPath);
                        undoStack.Push(currentPath);
                        StoreCurrentPathPaint();
                        redoStack.Clear();
                        redoPaintStack.Clear();
                        currentPath = null;
                    }
                    break;
            }
            e.Handled = true; // Stop further processing
        }


        private void PickerColor(object sender, PickedColorChangedEventArgs e)
        {
            UpdateCurrentPaint();
        }

        private void UpdateCurrentPaint()
        {
            this.kolor = MainColorPicker.PickedColor == null ? new Color(0, 0, 0) : MainColorPicker.PickedColor;
            byte red = (byte)(kolor.Red * 255);
            byte green = (byte)(kolor.Green * 255);
            byte blue = (byte)(kolor.Blue * 255);
            byte alpha = (byte)Math.Round(Przezroczystosc * 2.55, 0);

            currentPaint = new SKPaint
            {
                Color = new SKColor(red, green, blue, alpha),
                StrokeWidth = Thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
        }

        private void StoreCurrentPathPaint()
        {
            this.kolor = MainColorPicker.PickedColor==null?new Color(0,0,0): MainColorPicker.PickedColor;
            byte red = (byte)(kolor.Red * 255);
            byte green = (byte)(kolor.Green * 255);
            byte blue = (byte)(kolor.Blue * 255);
            byte alpha = (byte)Math.Round(Przezroczystosc * 2.55, 0);

            SKPaint pathPaint = new SKPaint
            {
                Color = new SKColor(red, green, blue, alpha),
                StrokeWidth = Thickness,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            allPaints.Add(pathPaint);
        }

        private void Move(object sender, EventArgs e)
        {
            this.isScrolling = !this.isScrolling;
        }
        private void Undo(object sender, EventArgs e)
        {
            // Check if the last action was text
            if (undoTextStack.Count > 0)
            {
                var lastTextObject = undoTextStack.Pop();
                redoTextStack.Push(lastTextObject);

                TextObjects.Remove(lastTextObject);

                CanvasView.InvalidateSurface(); // Redraw the canvas
            }
            // Otherwise, check if there are paths to undo
            else if (undoStack.Count > 0)
            {
                var lastPath = undoStack.Pop();
                var lastPaint = allPaints[allPaths.IndexOf(lastPath)];

                redoStack.Push(lastPath);
                redoPaintStack.Push(lastPaint);

                allPaths.Remove(lastPath);
                allPaints.Remove(lastPaint);

                CanvasView.InvalidateSurface(); // Redraw the canvas
            }
        }



        // Redo action
        private void Redo(object sender, EventArgs e)
        {
            // Redo text drawing if there's something to redo
            if (redoTextStack.Count > 0)
            {
                var lastTextObject = redoTextStack.Pop();
                undoTextStack.Push(lastTextObject);

                TextObjects.Add(lastTextObject);

                CanvasView.InvalidateSurface(); // Redraw the canvas after adding the text
            }
            // Redo path drawing if there's something to redo
            else if (redoStack.Count > 0)
            {
                var lastPath = redoStack.Pop();
                var lastPaint = redoPaintStack.Pop();

                // **Add to the lists before pushing to undo stacks**
                allPaths.Add(lastPath);
                allPaints.Add(lastPaint);

                undoStack.Push(lastPath);
                undoPaintStack.Push(lastPaint);

                CanvasView.InvalidateSurface(); // Redraw the canvas after adding the path
            }
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            string text = TextEntry.Text;

            if (!string.IsNullOrEmpty(text) && !TextObjects.Any(t => t.Text == text && t.Position == currentTextObject.Position))
            {
                // Create a new SKPaint object with updated properties
                SKPaint textPaint = new SKPaint
                {
                    Color = currentTextObject.Paint.Color,  // Use the current color
                    TextSize = FontSize,                     // Use the updated font size
                    IsAntialias = true
                };

                // Create a new TextObject
                TextObject newTextObject = new TextObject
                {
                    Text = text,
                    Position = currentTextObject.Position,
                    Paint = textPaint,
                    isSelected = false  // By default, the new text is not selected
                };

                // Push the new text object onto the undo stack
                undoTextStack.Push(newTextObject);

                // Add the new TextObject to the list
                TextObjects.Add(newTextObject);

                // Clear the text input for the next text input
                TextEntry.Text = string.Empty;

                // Invalidate the canvas to trigger a redraw
                CanvasView.InvalidateSurface();
            }
        }


        private void Slider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            this.FontSize=(float)e.NewValue;
            FontSizeLabel.Text = $"Rozmiar:{e.NewValue:F0}";
        }

        private void ToolbarItem_Clicked(object sender, EventArgs e)
        {
            TextEdit.IsVisible = !TextEdit.IsVisible;
        }
    }
}
