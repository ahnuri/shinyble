# HannaDemoApp - Architecture & Best Practices Guide

**Last Updated**: April 26, 2026  
**Version**: 1.0 - Production Release  

---

## Quick Reference: MVVM Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      USER INTERFACE                          │
│  (XAML - Pages, Controls, Converters, Templates)             │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ↓ (Binds to)
┌─────────────────────────────────────────────────────────────┐
│                    VIEW MODELS                               │
│  (ObservableObject, RelayCommand, [ObservableProperty])      │
│  - State Management                                          │
│  - Commands & Interactions                                   │
│  - Business Logic Coordination                               │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ↓ (Uses)
┌─────────────────────────────────────────────────────────────┐
│                    SERVICES                                  │
│  (IHNABleService, IHNADialogService, etc.)                   │
│  - Application-level functionality                           │
│  - Cross-cutting concerns                                    │
│  - External system integration                               │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ↓ (Uses)
┌─────────────────────────────────────────────────────────────┐
│                    MODELS                                    │
│  (HNABleDeviceModel, HNAMeasurementLogModel)                 │
│  - Pure data structures                                      │
│  - NO service dependencies                                   │
│  - Observable for binding                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Key Principles

### 1. Strict Separation of Concerns

**❌ WRONG**:
```csharp
public partial class MyPage : ContentPage
{
    private void OnButtonClicked()
    {
        // Direct API calls from code-behind
        var data = await _apiService.FetchData();
        myLabel.Text = data.ToString();
    }
}
```

**✅ CORRECT**:
```csharp
// XAML: <Button Command="{Binding FetchDataCommand}" />
public partial class MyViewModel : ObservableObject
{
    [RelayCommand]
    private async Task FetchData()
    {
        Data = await _apiService.FetchData();
    }

    [ObservableProperty]
    private DataModel? data;
}

public partial class MyPage : ContentPage
{
    public MyPage(MyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

---

### 2. Dependency Injection - Always Use Constructor Parameters

**❌ WRONG** (Service Locator Anti-Pattern):
```csharp
public MyViewModel()
{
    _bleService = ServiceProvider.GetService<IHNABleService>();
}
```

**✅ CORRECT** (Explicit Dependencies):
```csharp
public MyViewModel(IHNABleService bleService, IHNADialogService dialogService)
{
    _bleService = bleService ?? throw new ArgumentNullException(nameof(bleService));
    _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
}
```

---

### 3. Models Must NOT Depend on Services

**❌ CRITICAL VIOLATION**:
```csharp
public class MyModel
{
    private readonly IRepository _repository;
    
    public MyModel(IRepository repository) // ❌ Models don't take service dependencies!
    {
        _repository = repository;
    }
    
    public async Task Save()
    {
        await _repository.SaveAsync(this);
    }
}
```

**✅ CORRECT**:
```csharp
public class MyModel
{
    public string Name { get; set; }
    public int Value { get; set; }
    // Pure data - NO service dependencies
    
    public List<MyModel> ExtractForPersistence()
    {
        return new List<MyModel> { this };
    }
}

// Service handles persistence
public class MyService
{
    public async Task SaveModelAsync(MyModel model)
    {
        await _repository.SaveAsync(model.ExtractForPersistence());
    }
}
```

---

### 4. Async Operations - Never Fire and Forget

**❌ WRONG**:
```csharp
_ = SomeAsync OperationAsync(); // Exceptions silently ignored!
```

**✅ CORRECT**:
```csharp
// Option 1: Await in async method
private async Task OnButtonClick()
{
    try
    {
        await SomeAsyncOperationAsync();
    }
    catch (Exception ex)
    {
        await DialogService.ShowAlertAsync("Error", ex.Message);
    }
}

// Option 2: Use SafeFireAndForget for background operations
SomeAsyncOperationAsync().SafeFireAndForget("Operation name");
```

---

### 5. Null Safety & Validation

**❌ WRONG**:
```csharp
public void ProcessData(MyModel model)
{
    var value = model.Value; // Could be null!
    DoSomething(value);
}
```

**✅ CORRECT**:
```csharp
public void ProcessData(MyModel? model)
{
    ArgumentNullException.ThrowIfNull(model, nameof(model));
    var value = model.Value;
    DoSomething(value);
}

// Or for nullable properties
public void ProcessData(MyModel model)
{
    ArgumentNullException.ThrowIfNull(model, nameof(model));
    
    if (model.Value is null)
    {
        throw new ArgumentException("Value cannot be null", nameof(model));
    }
    
    DoSomething(model.Value);
}
```

---

### 6. ObservableObject with RelayCommand

**ALWAYS use Community Toolkit patterns**:

```csharp
public partial class MyViewModel : ObservableObject
{
    private readonly IMyService _service;

    [ObservableProperty]
    private string title = "Default Title";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid))]
    private string name;

    [ObservableProperty]
    private bool isBusy;

    public bool IsValid => !string.IsNullOrEmpty(Name);

    public MyViewModel(IMyService service)
    {
        _service = service;
    }

    [RelayCommand]
    private async Task SaveData()
    {
        if (!IsValid)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _service.SaveAsync(Name);
            Title = "Saved!";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

**XAML Usage**:
```xaml
<Button Text="Save" Command="{Binding SaveDataCommand}" IsEnabled="{Binding IsValid}" />
<ActivityIndicator IsRunning="{Binding IsBusy}" />
```

---

### 7. Collections & Bindings

**DO**:
- Use `ObservableCollection<T>` for UI binding
- Use `LINQ ToList()` before enumeration in loops
- Cache expensive computed properties

**DON'T**:
- Repeatedly enumerate same collection (LINQ evaluates on each access)
- Modify collections during iteration
- Use List<T> for UI binding (no auto-update notifications)

```csharp
// ❌ WRONG - Enumerates 3 times!
public int ItemCount => Items.Count();
public string Summary => $"Items: {Items.Count()}";
public bool HasItems => Items.Any();

// ✅ CORRECT - Single enumeration
private int _cachedCount;
private bool _countDirty = true;

public int ItemCount
{
    get
    {
        if (_countDirty)
        {
            _cachedCount = Items.Count;
            _countDirty = false;
        }
        return _cachedCount;
    }
}
```

---

## File Organization Standards

### Namespace Structure:
```
HannaDemoApp/
├── Core/
│   ├── Constants/        (HNAAppConstants.cs)
│   ├── Converters/       (IValueConverter implementations)
│   ├── DeviceHandlers/   (IHNADeviceHandler implementations)
│   └── Enums/            (HNAProductId.cs)
├── Features/
│   ├── Device/
│   │   ├── HNADevicePage.xaml
│   │   ├── HNADevicePage.xaml.cs
│   │   ├── HNADeviceViewModel.cs
│   │   └── LiveMeasure/
│   │       ├── LiveDetailsPage.xaml
│   │       ├── LiveDetailsPage.xaml.cs
│   │       └── LiveDetailsViewModel.cs
│   ├── Landing/
│   ├── LogDetail/
│   ├── LogHistory/
│   └── UserSettings/
├── Models/
│   ├── HNABleDeviceModel.cs      (Pure data model)
│   └── HNAMeasurementLogModel.cs  (Pure data model)
├── Services/
│   ├── Ble/
│   │   ├── IHNABleService.cs
│   │   ├── HNABleService.cs
│   │   ├── IHNAProductMatchingService.cs
│   │   ├── HNAProductMatchingService.cs
│   │   └── AsyncTaskExtensions.cs
│   ├── Database/
│   ├── Dialog/
│   ├── Navigation/
│   └── Background/
└── Platforms/
    ├── Android/
    ├── iOS/
    ├── MacCatalyst/
    └── Windows/
```

---

## Creating a New Feature (Step-by-Step)

### Step 1: Create the ViewModel
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HannaDemoApp.Features.MyFeature;

/// <summary>
/// ViewModel for the MyFeature page.
/// Manages state, user interactions, and business logic.
/// </summary>
public partial class MyFeatureViewModel : ObservableObject
{
    private readonly IMyService _myService;

    [ObservableProperty]
    private string title = "My Feature";

    [ObservableProperty]
    private bool isBusy;

    public MyFeatureViewModel(IMyService myService)
    {
        _myService = myService ?? throw new ArgumentNullException(nameof(myService));
    }

    [RelayCommand]
    private async Task DoSomething()
    {
        IsBusy = true;
        try
        {
            await _myService.DoWorkAsync();
            Title = "Done!";
        }
        catch (Exception ex)
        {
            Title = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

### Step 2: Create the XAML Page
```xaml
<?xml version="1.0" encoding="utf-8" ?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             x:Class="HannaDemoApp.Features.MyFeature.MyFeaturePage"
             Title="My Feature">
    <VerticalStackLayout Padding="16" Spacing="16">
        <Label Text="{Binding Title}" FontSize="24" FontAttributes="Bold"/>
        <Button Text="Do Something" Command="{Binding DoSomethingCommand}" />
        <ActivityIndicator IsRunning="{Binding IsBusy}" />
    </VerticalStackLayout>
</ContentPage>
```

### Step 3: Create the Code-Behind
```csharp
namespace HannaDemoApp.Features.MyFeature;

/// <summary>
/// View for MyFeature. Pure MVVM with all logic in ViewModel.
/// </summary>
public partial class MyFeaturePage : ContentPage
{
    public MyFeaturePage(MyFeatureViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

### Step 4: Register in MauiProgram.cs
```csharp
// ViewModel
builder.Services.AddTransient<MyFeatureViewModel>();

// Page
builder.Services.AddTransient<MyFeaturePage>();
```

### Step 5: Add Navigation Route
```csharp
// In AppShell.xaml.cs or navigation constants
Routing.RegisterRoute(nameof(MyFeaturePage), typeof(MyFeaturePage));
```

---

## Common Pitfalls to Avoid

### Pitfall 1: Code-Behind Business Logic
```csharp
// ❌ DON'T
private void OnButtonClicked()
{
    var result = await _complexAlgorithm();
    _dataStore.Save(result);
    RefreshUI();
}

// ✅ DO
[RelayCommand]
private async Task OnButtonClicked()
{
    // In ViewModel with injected dependencies
}
```

### Pitfall 2: Model Service Dependencies
```csharp
// ❌ DON'T
public class Item
{
    private IRepository _repo;
    public async Task Save() => await _repo.SaveAsync(this);
}

// ✅ DO
public class Item { }
// Service handles: await repo.SaveAsync(item);
```

### Pitfall 3: Fire-and-Forget Without Tracking
```csharp
// ❌ DON'T
_ = SomeAsyncOperation();

// ✅ DO
await SomeAsyncOperation(); // Or use SafeFireAndForget with logging
```

### Pitfall 4: View Models Updating UI Directly
```csharp
// ❌ DON'T
public class MyViewModel
{
    public void UpdateUI()
    {
        _myLabel.Text = "Updated"; // Can't access UI from ViewModel!
    }
}

// ✅ DO
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    private string labelText = "Updated"; // Bind to this
}
```

---

## Testing Standards

### ViewModel Unit Tests
```csharp
[TestFixture]
public class MyViewModelTests
{
    private MyViewModel _viewModel;
    private Mock<IMyService> _mockService;

    [SetUp]
    public void Setup()
    {
        _mockService = new Mock<IMyService>();
        _viewModel = new MyViewModel(_mockService.Object);
    }

    [Test]
    public async Task DoSomething_CallsServiceCorrectly()
    {
        // Arrange
        var expected = "expected result";
        _mockService.Setup(s => s.DoWorkAsync()).ReturnsAsync(expected);

        // Act
        await _viewModel.DoSomethingCommand.ExecuteAsync(null);

        // Assert
        _mockService.Verify(s => s.DoWorkAsync(), Times.Once);
        Assert.That(_viewModel.Title, Is.EqualTo("Done!"));
    }
}
```

---

## Code Review Checklist

When submitting pull requests, verify:

- [ ] ✅ Views have NO business logic (only XAML + binding)
- [ ] ✅ Code-behind only has binding context setup
- [ ] ✅ ViewModels inherit from `ObservableObject`
- [ ] ✅ Commands use `[RelayCommand]` attribute
- [ ] ✅ State uses `[ObservableProperty]` attribute
- [ ] ✅ All async operations properly awaited or tracked
- [ ] ✅ Models have NO service dependencies
- [ ] ✅ Services injected via constructor parameters
- [ ] ✅ Parameters validated with `ArgumentNullException`, `ArgumentException`
- [ ] ✅ Error handling with try-catch in async commands
- [ ] ✅ XML documentation on public members
- [ ] ✅ Unit tests included for complex logic
- [ ] ✅ No TODO comments (track in Azure Devops/GitHub Issues)

---

## Performance Best Practices

1. **Lazy Load Collections**
   - Don't load all items upfront
   - Implement pagination or virtual scrolling

2. **Cache Expensive Operations**
   - Use ObservableProperty to cache computed values
   - Invalidate cache only when dependencies change

3. **Batch UI Updates**
   - Use `ObservableRangeCollection` from Toolkit
   - Avoid adding items one-by-one in loops

4. **Dispose Resources Properly**
   - Implement `IDisposable` on ViewModels that need cleanup
   - Unsubscribe from events in `OnDisappearing`
   - Use `using` statements for disposable resources

---

## References

- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [MVVM Toolkit](https://github.com/CommunityToolkit/dotnet)
- [Clean Architecture Principles](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)

---

## Questions or Improvements?

This guide is living documentation. Please contribute improvements via pull requests or discussions.

**Last Review**: April 26, 2026  
**Next Review**: When major architectural changes are needed
