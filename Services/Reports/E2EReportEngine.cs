using CallMan.Data;
using CallMan.Interfaces.Reports;
using CallMan.Models.Enums;
using CallMan.Services.Reports.Strategies;
using System.Data;

namespace CallMan.Services.Reports
{
    public class E2EReportEngine
    {
        private readonly CrmDbContext _dbFactory;
        private readonly Dictionary<string, IE2EReportStrategy> _strategyRegistry = new();

        public E2EReportEngine(CrmDbContext dbFactory)
        {
            _dbFactory = dbFactory;

            // Register your defined matrix options right here
            Register(new SalesCustomerStrategy());
            Register(new ItemsPurchaseStrategy());
            Register(new PLSalesStrategy());
            Register(new SalesPaymentsStrategy());
            Register(new SalesLocationStrategy());
            Register(new SalesStaffStrategy());
            Register(new SalesBusinessStrategy());
        }

        /// <summary>
        /// Generates a standardized unique tracking key string from enum selection profiles.
        /// </summary>
        private string BuildMatrixKey(E2EMainFilter main, E2EComparisonTarget target)
        {
            // Generates keys like: "Sales_Customer", "Purchases_Vendors", etc.
            return $"{main}_{target}";
        }

        /// <summary>
        /// Dynamically registers a concrete analytical report strategy into the centralized execution routing hub.
        /// </summary>
        /// <param name="strategy">The target reporting matrix strategy class instance.</param>
        private void Register(IE2EReportStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));

            // Loop through all possible Enum permutations to see which combination this strategy supports
            foreach (E2EMainFilter main in Enum.GetValues(typeof(E2EMainFilter)))
            {
                foreach (E2EComparisonTarget target in Enum.GetValues(typeof(E2EComparisonTarget)))
                {
                    string strategyKey = strategy.GetStrategyKey(main, target);

                    // If the strategy returns a valid, populated key for this intersection, register it
                    if (!string.IsNullOrWhiteSpace(strategyKey))
                    {
                        // Safeguard against accidental duplicate developer mapping registrations
                        if (_strategyRegistry.ContainsKey(strategyKey))
                        {
                            throw new InvalidOperationException($"Architecture Conflict: A strategy with the key '{strategyKey}' has already been registered in the E2E engine tracking collection.");
                        }

                        _strategyRegistry.Add(strategyKey, strategy);
                    }
                }
            }
        }

        public async Task<DataTable> ExecuteMatrixQueryAsync(E2EMainFilter main, E2EComparisonTarget target, DateTime from, DateTime to)
        {
            string key = BuildMatrixKey(main, target);
            if (!_strategyRegistry.TryGetValue(key, out var strategy))
            {
                throw new NotSupportedException($"The intersection report format for {main} compared with {target} is currently being compiled.");
            }

            using var db = _dbFactory.CreateConnection();
            return await strategy.RunQueryAsync(db, from, to);
        }
    }
}
