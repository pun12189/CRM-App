using CallMan.Data;
using CallMan.Interfaces;
using CallMan.Services;
using CallMan.ViewModels;
using CallMan.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;

namespace CallMan
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider? ServiceProvider { get; private set; }

        public App()
        {
#if DEBUG

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            string connectionString = "Server=82.29.166.165;Port=3306;Uid=root;Pwd=sofricdev;database=callmandev";
#endif
#if RELEASE

            //_connectionString = "Server=192.168.1.90;Uid=cosdb;Pwd=Cosmetify@123;database=cosmetify";
            string connectionString = "Server=82.29.166.165;Port=3307;Uid=root;Pwd=sofricprod;database=callmanprod";
#endif
#if TESTING

            //_connectionString = "DataSource=bahikitab-aws.c3s6wewcwox1.us-east-1.rds.amazonaws.com;Port=3306;Uid=admin;Pwd=Il6oOvguA2SB5IEQxWCJ;database=bahikitab";
            string connectionString = "Server=82.29.166.165;Port=3306;Uid=root;Pwd=sofricdev;database=callmandev";
#endif
            var services = new ServiceCollection();
            
            services.AddSingleton(new CrmDbContext(connectionString));

            // 2. Register Services (They will now receive the DbContext)
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<LeadService>(); // Our Lead Management service

            // 3. VIEWMODELS (State Layer)
            services.AddSingleton<MainViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<LeadViewModel>();
            services.AddTransient<AddLeadDialogViewModel>();
            services.AddTransient<MaturedLeadsViewModel>(); 
            services.AddTransient<AllOrdersViewModel>();

            // Views/Modules
            services.AddTransient<DashboardViewModel>();

            // 4. Register Views
            services.AddTransient<LoginView>();
            services.AddTransient<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            var loginView = ServiceProvider!.GetRequiredService<LoginView>();
            loginView.Show();
        }
    }

}
