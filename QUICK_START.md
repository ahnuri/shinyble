# HannaDemoApp - Quick Start Guide for Developers

**Last Updated**: April 26, 2026

---

## 🚀 Getting Started

### 1. Clone and Build
```bash
cd HannaDemoApp/HannaDemoApp
dotnet restore
dotnet build -f net10.0-ios -r ios-arm64  # or your target platform
```

### 2. Understand the Project Structure
```
Features/          ← User features (Pages + ViewModels)
├── Device/        ← Device management
├── Landing/       ← Home page
├── LogDetail/     ← Individual log viewing
└── LogHistory/    ← Log browsing & management

Services/          ← Business logic layer
├── Ble/           ← Bluetooth Low Energy
├── Database/      ← Data persistence
├── Dialog/        ← User dialogs & alerts
├── Navigation/    ← App routing
└── Background/    ← Background tasks

Core/              ← Shared utilities
├── Constants/
├── Converters/    ← XAML value converters
├── DeviceHandlers/← Device-specific handling
└── Enums/

Models/            ← Pure data (NO service dependencies)
```

---

## 📋 Core Principles (READ FIRST!)

### ❌ NEVER DO THIS:
1. **Put business logic in code-behind** (Pages should only have `BindingContext = viewModel`)
2. **Have models depend on services** (Models are pure data)
3. **Fire-and-forget async tasks** (Use `await` or `.SafeFireAndForget()`)
4. **Update UI from ViewModel directly** (Use property bindings)
5. **Manual event wiring** (Use data binding in XAML)

### ✅ ALWAYS DO THIS:
1. **Use ViewModels for state and commands**
2. **Use `[ObservableProperty]` for bindable data**
3. **Use `[RelayCommand]` for user actions**
4. **Use `ObservableCollection<T>` for UI lists**
5. **Use constructor dependency injection**

---

## 📝 Creating a New Feature

### Step 1: Create the ViewModel
```csharp
public partial class MyFeatureViewModel : ObservableObject
{
    private readonly IMyService _service;

    [ObservableProperty]
    private string title = "My Feature";

    public MyFeatureViewModel(IMyService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    [RelayCommand]
    private async Task LoadData()
    {
        Title = await _service.GetTitleAsync();
    }
}
```

### Step 2: Create the XAML Page
```xaml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             x:Class="HannaDemoApp.Features.MyFeature.MyFeaturePage">
    <VerticalStackLayout Padding="16">
        <Label Text="{Binding Title}" FontSize="20" FontAttributes="Bold"/>
        <Button Text="Load" Command="{Binding LoadDataCommand}"/>
    </VerticalStackLayout>
</ContentPage>
```

### Step 3: Code-Behind (Minimal!)
```csharp
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
builder.Services.AddTransient<MyFeatureViewModel>();
builder.Services.AddTransient<MyFeaturePage>();
```

---

## 🔧 Common Tasks

### Add a Property to ViewModel
```csharp
[ObservableProperty]
private string myProperty = "default";
```
✅ This auto-implements: `INotifyPropertyChanged`, getters, setters

### Handle a Button Click
```csharp
[RelayCommand]
private async Task OnButtonClick()
{
    // Business logic here
}
```
✅ In XAML: `<Button Command="{Binding OnButtonClickCommand}"/>`

### Load Data in a List
```csharp
[ObservableProperty]
private ObservableCollection<MyItem> items = new();

[RelayCommand]
private async Task LoadItems()
{
    var loadedItems = await _service.GetItemsAsync();
    Items.Clear();
    foreach (var item in loadedItems)
        Items.Add(item);
}
```
✅ In XAML: `<CollectionView ItemsSource="{Binding Items}"/>`

### Show an Alert
```csharp
[RelayCommand]
private async Task ShowAlert()
{
    await _dialogService.ShowAlertAsync(
        "Title", 
        "Message", 
        "OK");
}
```

### Handle Errors
```csharp
[RelayCommand]
private async Task DoSomething()
{
    try
    {
        await _service.DoWorkAsync();
    }
    catch (Exception ex)
    {
        await _dialogService.ShowAlertAsync(
            "Error", 
            $"Something went wrong: {ex.Message}", 
            "OK");
    }
}
```

---

## 🧪 Writing Tests

```csharp
[TestFixture]
public class MyFeatureViewModelTests
{
    private MyFeatureViewModel _viewModel;
    private Mock<IMyService> _mockService;

    [SetUp]
    public void Setup()
    {
        _mockService = new Mock<IMyService>();
        _viewModel = new MyFeatureViewModel(_mockService.Object);
    }

    [Test]
    public async Task LoadData_UpdatesTitle()
    {
        // Arrange
        _mockService.Setup(s => s.GetTitleAsync())
            .ReturnsAsync("Test Title");

        // Act
        await _viewModel.LoadDataCommand.ExecuteAsync(null);

        // Assert
        Assert.That(_viewModel.Title, Is.EqualTo("Test Title"));
    }
}
```

---

## 📚 Key Concepts

### ObservableObject
```csharp
// Base class for all ViewModels
public partial class MyViewModel : ObservableObject { }
```

### ObservableProperty
```csharp
[ObservableProperty]
private string name = "default";
// Creates: Name property, PropertyChanged notifications, binding support
```

### RelayCommand
```csharp
[RelayCommand]
private async Task MyCommand() { }
// Creates: MyCommandCommand (IAsyncRelayCommand<T>), binding support, CanExecute logic
```

### ObservableCollection
```csharp
// ALWAYS use for UI binding (not List<T>)
public ObservableCollection<Item> Items { get; } = new();
// Automatically notifies UI of Add/Remove/Clear operations
```

### Converters
```xaml
<!-- Convert bool to visibility, color to text, etc. -->
<Label IsVisible="{Binding IsBusy, Converter={StaticResource BoolToOpacityConverter}}"/>
```

---

## 🔍 Debugging Tips

### Debug Output
```csharp
System.Diagnostics.Debug.WriteLine($"Variable: {variable}");
```

### Check Bindings
```xaml
<!-- Text="{Binding Title, StringFormat='Title: {0}'}" -->
<!-- Allows you to see if binding is working -->
```

### Async Errors
```csharp
// View output window in Visual Studio for error logs from SafeFireAndForget
[AsyncError] Device initialization after connection failed: Connection refused
```

---

## 📖 Documentation Files

- **[ARCHITECTURE_GUIDE.md](ARCHITECTURE_GUIDE.md)** - Comprehensive best practices
- **[CODE_QUALITY_REPORT.md](CODE_QUALITY_REPORT.md)** - Detailed improvements made
- **[IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)** - What was changed and why

---

## ⚠️ Common Mistakes

### Mistake 1: Code-Behind Logic
```csharp
// ❌ WRONG
private void OnButtonClicked()
{
    var data = await _service.FetchData();
    myLabel.Text = data.ToString();
}

// ✅ CORRECT - Use ViewModel instead
```

### Mistake 2: Not Validating Parameters
```csharp
// ❌ WRONG
public MyViewModel(IService service)
{
    _service = service; // Could be null!
}

// ✅ CORRECT
public MyViewModel(IService service)
{
    _service = service ?? throw new ArgumentNullException(nameof(service));
}
```

### Mistake 3: Forgetting to Register Services
```csharp
// In MauiProgram.cs
builder.Services.AddTransient<MyViewModel>();
builder.Services.AddTransient<MyPage>();
```

### Mistake 4: Using List Instead of ObservableCollection
```csharp
// ❌ WRONG
public List<Item> Items { get; } = new();

// ✅ CORRECT
public ObservableCollection<Item> Items { get; } = new();
```

---

## 🆘 Need Help?

1. **Check the Architecture Guide**: [ARCHITECTURE_GUIDE.md](ARCHITECTURE_GUIDE.md)
2. **Look at existing features**: Features/ folder has complete examples
3. **Review the code structure**: Services/ shows dependency patterns
4. **Search for similar patterns**: Find existing commands/properties as reference

---

## ✅ Pre-Commit Checklist

Before pushing code:

- [ ] ✅ No code-behind business logic
- [ ] ✅ ViewModels use `[ObservableProperty]` and `[RelayCommand]`
- [ ] ✅ Constructor parameters validated
- [ ] ✅ Async operations properly awaited
- [ ] ✅ Error handling with try-catch
- [ ] ✅ XML documentation on public members
- [ ] ✅ Unit tests included
- [ ] ✅ No TODO comments (use issues instead)
- [ ] ✅ Project builds without errors
- [ ] ✅ XAML binding properties exist in ViewModel

---

**Happy Coding! 🎉**

For questions about architecture or patterns, refer to [ARCHITECTURE_GUIDE.md](ARCHITECTURE_GUIDE.md)
