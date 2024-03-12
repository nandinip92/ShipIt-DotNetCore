using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using ShipIt.Exceptions;

namespace ShipIt.Models.ApiModels
{
    public class OutboundOrderResponseModel
    {
        public float TotalOrderWeightInKgs { get; set; }
        public int NumberOfTrucks { get; set; }

        public override String ToString()
        {
            return new StringBuilder()
                .AppendFormat("TotalOrderWeightInKgs: {0}, ", TotalOrderWeightInKgs)
                .AppendFormat("NumberOfTrucks: {0}", NumberOfTrucks)
                .ToString();
        }
    }

    public class StockAlterationWithWeights
    {
        public int ProductId { get; set; }
        public float Weight { get; set; }
        public int Quantity { get; set; }

        public StockAlterationWithWeights(int productId, int quantity, float weight)
        {
            this.ProductId = productId;
            this.Quantity = quantity;
            this.Weight = weight;

            if (quantity < 0)
            {
                throw new MalformedRequestException("Alteration must be positive");
            }
        }
    }
}
