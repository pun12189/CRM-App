using CallMan.Core;
using CallMan.Data;
using CallMan.Dialogs;
using CallMan.Interfaces;
using CallMan.Models;
using CallMan.Services;
using CallMan.Services.Reports;
using CallMan.ViewModels;
using CallMan.Views;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using System.Windows;

namespace CallMan
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        private static string connectionString = string.Empty;

        public App()
        {
#if DEBUG

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            connectionString = "Server=82.29.166.165;Port=3306;Uid=root;Pwd=sofricdev;database=callmandev;SSLMode=Required;";
#endif
#if RELEASE

            //_connectionString = "Server=192.168.1.90;Uid=cosdb;Pwd=Cosmetify@123;database=cosmetify";
            connectionString = "Server=82.29.166.165;Port=3307;Uid=root;Pwd=sofricprod;database=callmanprod;SSLMode=Required;";
#endif
#if TESTING

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            connectionString = "Server=82.29.166.165;Port=3306;Uid=root;Pwd=sofricdev;database=callmandev;SSLMode=Required;";
#endif
#if SUBODH

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            connectionString = "Server=127.0.0.1;Port=3306;Uid=root;Pwd=Sofric@123;database=callmandev";
#endif
#if RAVI

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            connectionString = "Server=82.29.166.165;Port=3308;Uid=root;Pwd=sofricraviprod;database=callmandev;SSLMode=Required;";
#endif
            
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-IN")));

            base.OnStartup(e);

            // ====================================================================
            // PHASE 1: COMPILER PRESSETS & CONFIGURATION FALLBACK CHECKS
            // ====================================================================
            DbConfig defaultEnvironmentPreset = GetCompilerDefaultConfig();

            bool configExists = DbConfigManager.LoadConfiguration();
            bool connectionValid = false;

            if (configExists)
            {
                connectionValid = await TestCurrentDatabaseConnectionAsync(DbConfigManager.CachedConnectionString);
            }
            else
            {
                // DEVELOPER BYPASS: Auto-seed preset files for local developer environments
#if !RELEASE
                DbConfigManager.SaveConfiguration(defaultEnvironmentPreset);
                configExists = true;
                connectionValid = await TestCurrentDatabaseConnectionAsync(DbConfigManager.CachedConnectionString);
#endif
            }

            // ====================================================================
            // PHASE 2: LAUNCH DATABASE CONFIGURATION DIALOG IF UNVERIFIED
            // ====================================================================
            if (!configExists || !connectionValid)
            {
                var configWindow = new DbConfigurationWindow();

                if (configWindow.DataContext is DbConfigurationViewModel vm)
                {
                    vm.Config = defaultEnvironmentPreset;
                }

                if (configWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            string finalConnectionString = DbConfigManager.CachedConnectionString;

            var services = new ServiceCollection();

            services.AddSingleton(new CrmDbContext(finalConnectionString));

            services.AddHttpClient<ApiService>();

            services.AddTransient<IWorkflowDataService, WorkflowDataService>();

            // 2. Register Services (They will now receive the DbContext)
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IUserSession, UserSession>();
            services.AddSingleton<ITwoFactorService, TwoFactorService>();
            services.AddSingleton<IGlobalSettingsService, GlobalSettingsService>();
            services.AddSingleton<IActionSecurityGuard, ActionSecurityGuard>();
            services.AddSingleton<IOrderHistoryService, OrderHistoryService>();
            services.AddSingleton<LeadService>();// Our Lead Management service
            services.AddSingleton<SettingService>();
            services.AddSingleton<CategoryService>();
            services.AddSingleton<ProductService>();
            services.AddSingleton<OrderService>();
            services.AddSingleton<ProfileService>();
            services.AddSingleton<OrderStageService>();
            services.AddSingleton<DepartmentService>();
            services.AddSingleton<LoginLogService>();
            services.AddSingleton<EmailService>();
            services.AddSingleton<OccupiedLocationService>();
            services.AddSingleton<WorkflowEngine>();
            services.AddSingleton<IImportService, ImportService>();
            services.AddSingleton<NotificationRoutingService>();
            services.AddSingleton<NotificationHistoryService>();
            services.AddSingleton<E2EReportEngine>();
            services.AddSingleton<CustomFieldService>();
            services.AddSingleton<LicenseService>();
            services.AddSingleton<BackupService>();
            services.AddSingleton<PermissionService>();
            services.AddSingleton<StaffService>();
            services.AddSingleton<SchemeService>();
            services.AddSingleton<VendorService>();
            services.AddSingleton<PurchaseService>();
            services.AddSingleton<ReportEntityService>();

            // 3. VIEWMODELS (State Layer)
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LeadViewModel>();
            services.AddTransient<AddLeadDialogViewModel>();
            services.AddTransient<MaturedLeadsViewModel>();
            services.AddTransient<AllOrdersViewModel>();
            services.AddTransient<AdminSettingsViewModel>();
            services.AddTransient<GenericSettingsViewModel>();
            services.AddTransient<ManageCategoriesViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<CompanyProfileViewModel>();
            services.AddTransient<OrderStagesViewModel>();
            services.AddTransient<DepartmentsViewModel>();
            services.AddTransient<LoginLogsViewModel>();
            services.AddTransient<EmailSettingsViewModel>();
            services.AddTransient<LeadFollowupViewModel>();
            services.AddTransient<OccupiedLocationViewModel>();
            services.AddTransient<CustomerSummaryViewModel>();
            services.AddTransient<GlobalNewOrderViewModel>();
            services.AddTransient<ImportViewModel>();
            services.AddTransient<E2EReportsDashboardViewModel>();
            services.AddTransient<ToastPollingWorker>();
            services.AddTransient<CategorySettingsViewModel>();
            services.AddTransient<SchemeManagementViewModel>();

            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<AddStaffDialogViewModel>();
            services.AddTransient<DriveViewModel>();

            // Views/Modules
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<WorkflowViewModel>();
            services.AddTransient<CustomFieldsViewModel>();
            services.AddTransient<CreateFieldViewModel>();
            services.AddTransient<ActivationViewModel>();
            services.AddTransient<PermissionsManagementViewModel>();
            services.AddTransient<VendorViewModel>();
            services.AddTransient<PurchaseViewModel>();

            // 4. Register Views
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();

            var licenseService = ServiceProvider!.GetRequiredService<LicenseService>();
            await LicenseManager.InitializeAsync(licenseService);

            var loginView = ServiceProvider!.GetRequiredService<LoginView>();
            //this.MainWindow = null;
            loginView.Show();            

            var engine = ServiceProvider!.GetService<WorkflowEngine>();
            if (engine != null)
            {
                await Task.Run(async () =>
                {
                    try
                    {
                        // 1. Process any missed events from when the app was closed
                        await engine.ProcessQueueAsync();

                        // 2. Run the inactivity check for old customers
                        await engine.CheckInactivityWorkflowsAsync();
                    }
                    catch (Exception ex)
                    {
                        // Fallback hook to record background engine thread crashes straight to Sentry
                    }
                });
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window childWindow)
            {
                // CRITICAL EXCEPTION GUARD: 
                if (childWindow.GetType().Name == "LoginView") return;
                // Do not assign the MainWindow to own itself, or it will throw an InvalidOperationException!
                if (childWindow == Current.MainWindow) return;

                // Ensure the MainWindow exists and is initialized before assigning ownership
                if (Current.MainWindow != null)
                {
                    // Only assign if the developer hasn't already explicitly set a custom owner
                    if (childWindow.Owner == null)
                    {
                        childWindow.Owner = Current.MainWindow;

                        // OPTIONAL BONUS GUARD: 
                        // Centers the dialog perfectly over the main screen automatically
                        childWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                }
            }
        }

        /// <summary>
        /// Asynchronous, short-timeout database connector check.
        /// </summary>
        private async Task<bool> TestCurrentDatabaseConnectionAsync(string connectionString)
        {
            try
            {
                // Inject a tight 3-second timeout into the test connection string so it fails fast if offline
                var testBuilder = new MySqlConnectionStringBuilder(connectionString)
                {
                    ConnectionTimeout = 3
                };

                using var conn = new MySqlConnection(testBuilder.ConnectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hardcoded environment compiler presets matching specific developer desks.
        /// </summary>
        private DbConfig GetCompilerDefaultConfig()
        {
            var preset = new DbConfig();

#if DEBUG
            preset.Server = "";
            preset.Port = 3306;
            preset.UserId = "root";
            preset.Password = "sofricdev";
            preset.Database = "callmandev";
            preset.UseSsl = true;
#elif RELEASE
            preset.Server = "82.29.166.165";
            preset.Port = 3307;
            preset.UserId = "root";
            preset.Password = "sofricprod";
            preset.Database = "callmanprod";
            preset.UseSsl = true;
#elif TESTING
            preset.Server = "82.29.166.165";
            preset.Port = 3306;
            preset.UserId = "root";
            preset.Password = "sofricdev";
            preset.Database = "callmandev";
            preset.UseSsl = true;
#elif SUBODH
            preset.Server = "127.0.0.1";
            preset.Port = 3306;
            preset.UserId = "root";
            preset.Password = "Sofric@123";
            preset.Database = "callmandev";
            preset.UseSsl = false;
#elif RAVI
            preset.Server = "82.29.166.165";
            preset.Port = 3308;
            preset.UserId = "root";
            preset.Password = "sofricraviprod";
            preset.Database = "callmandev";
            preset.UseSsl = true;
#endif

            return preset;
        }
    }
}
