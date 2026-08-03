using Tijori.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Core
{
    public static class VendorRatingCalculator
    {
        public static (double Rating, double OnTimeRate, int AvgDelayDays) CalculateRating(IEnumerable<PurchaseOrder> vendorOrders)
        {
            var receivedOrders = vendorOrders.Where(o => o.OrderStatus == "Received").ToList();

            if (!receivedOrders.Any())
            {
                return (5.0, 100.0, 0); // Default score for new vendors without past order history
            }

            int totalOrders = receivedOrders.Count;
            int delayedOrdersCount = receivedOrders.Count(o => o.IsDelayed);
            int totalDelayDays = receivedOrders.Sum(o => o.DelayInDays);

            double onTimeRate = ((double)(totalOrders - delayedOrdersCount) / totalOrders) * 100.0;
            double avgDelayDays = (double)totalDelayDays / totalOrders;

            // Penalty Deduction Logic
            double delayPenalty = (avgDelayDays * 0.4) + ((double)delayedOrdersCount / totalOrders * 1.5);
            double finalRating = Math.Max(1.0, Math.Min(5.0, 5.0 - delayPenalty));

            return (Math.Round(finalRating, 1), Math.Round(onTimeRate, 1), (int)Math.Ceiling(avgDelayDays));
        }
    }
}
