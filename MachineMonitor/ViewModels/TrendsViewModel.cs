using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MachineMonitor.Services;
using SkiaSharp;
using System;
using System.Collections.ObjectModel;

namespace MachineMonitor.ViewModels;

public partial class TrendsViewModel : ViewModelBase
{
    private const int MaxPoints = 60;

    private readonly ILogService _logService;

    public ObservableCollection<ObservableValue> TempValues  { get; } = new();
    public ObservableCollection<ObservableValue> PressValues { get; } = new();
    public ObservableCollection<ObservableValue> MotorValues { get; } = new();

    public ISeries[] TempSeries  { get; }
    public ISeries[] PressSeries { get; }
    public ISeries[] MotorSeries { get; }

    public Axis[] TempYAxes  { get; } = [new Axis { MinLimit = 0,    MaxLimit = 130,  LabelsPaint = new SolidColorPaint(new SKColor(0x4A, 0x55, 0x68)) }];
    public Axis[] PressYAxes { get; } = [new Axis { MinLimit = 0,    MaxLimit = 12,   LabelsPaint = new SolidColorPaint(new SKColor(0x4A, 0x55, 0x68)) }];
    public Axis[] MotorYAxes { get; } = [new Axis { MinLimit = 0,    MaxLimit = 2000, LabelsPaint = new SolidColorPaint(new SKColor(0x4A, 0x55, 0x68)) }];
    public Axis[] XAxes      { get; } = [new Axis { IsVisible = false }];

    public TrendsViewModel(ILogService logService)
    {
        _logService = logService;

        TempSeries = [new LineSeries<ObservableValue>
        {
            Values       = TempValues,
            Stroke       = new SolidColorPaint(new SKColor(0xFF, 0x6B, 0x35), 2),
            Fill         = new LinearGradientPaint(
                               [new SKColor(0xFF, 0x6B, 0x35, 70), new SKColor(0xFF, 0x6B, 0x35, 0)],
                               new SKPoint(0.5f, 0f), new SKPoint(0.5f, 1f)),
            GeometrySize = 0,
        }];

        PressSeries = [new LineSeries<ObservableValue>
        {
            Values       = PressValues,
            Stroke       = new SolidColorPaint(new SKColor(0x00, 0xB4, 0xD8), 2),
            Fill         = new LinearGradientPaint(
                               [new SKColor(0x00, 0xB4, 0xD8, 70), new SKColor(0x00, 0xB4, 0xD8, 0)],
                               new SKPoint(0.5f, 0f), new SKPoint(0.5f, 1f)),
            GeometrySize = 0,
        }];

        MotorSeries = [new LineSeries<ObservableValue>
        {
            Values       = MotorValues,
            Stroke       = new SolidColorPaint(new SKColor(0x7B, 0x2F, 0xBE), 2),
            Fill         = new LinearGradientPaint(
                               [new SKColor(0x7B, 0x2F, 0xBE, 70), new SKColor(0x7B, 0x2F, 0xBE, 0)],
                               new SKPoint(0.5f, 0f), new SKPoint(0.5f, 1f)),
            GeometrySize = 0,
        }];

        logService.ReadingAdded += OnReadingAdded;
    }

    private void OnReadingAdded(object? sender, EventArgs e)
    {
        var readings = _logService.GetReadings();
        if (readings.Count == 0) return;
        var last = readings[readings.Count - 1];

        Dispatcher.UIThread.Post(() =>
        {
            Append(TempValues,  last.Temperature);
            Append(PressValues, last.Pressure);
            Append(MotorValues, last.MotorSpeed);
        });
    }

    private static void Append(ObservableCollection<ObservableValue> col, double value)
    {
        col.Add(new ObservableValue(value));
        if (col.Count > MaxPoints)
            col.RemoveAt(0);
    }
}
