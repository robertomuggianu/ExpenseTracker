using Microsoft.Extensions.Logging;

using ExpenseTracker.Application;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Views;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace ExpenseTracker;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseSkiaSharp()
			.UseLiveCharts(settings => settings.AddDarkTheme())
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<IExpenseRepository>(_ =>
			new SqliteExpenseRepository(Path.Combine(FileSystem.AppDataDirectory, "expense-tracker.db3")));
		builder.Services.AddSingleton<AccountService>();
		builder.Services.AddSingleton<CategoryService>();
		builder.Services.AddSingleton<TransactionService>();
		builder.Services.AddSingleton<ReportingService>();
		builder.Services.AddSingleton<ICsvExportService, CsvExportService>();
		builder.Services.AddSingleton<ITransactionImportParser, TransactionImportParser>();
		builder.Services.AddSingleton<TransactionImportService>();
		builder.Services.AddSingleton<IUserDialogService, UserDialogService>();
		builder.Services.AddSingleton<IAppNavigationService, AppNavigationService>();
		builder.Services.AddSingleton<IFileShareService, FileShareService>();
		builder.Services.AddSingleton<IImportFilePickerService, ImportFilePickerService>();

		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<TransactionsViewModel>();
		builder.Services.AddTransient<TransactionEditorViewModel>();
		builder.Services.AddTransient<StatisticsViewModel>();
		builder.Services.AddTransient<AccountsViewModel>();
		builder.Services.AddTransient<CategoriesViewModel>();
		builder.Services.AddTransient<ExportViewModel>();

		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<TransactionsPage>();
		builder.Services.AddTransient<TransactionEditorPage>();
		builder.Services.AddTransient<StatisticsPage>();
		builder.Services.AddTransient<AccountsPage>();
		builder.Services.AddTransient<CategoriesPage>();
		builder.Services.AddTransient<ExportPage>();
		builder.Services.AddTransient<MorePage>();
		builder.Services.AddSingleton<AppShell>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
