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
        public List<Truck> Truck { get; set; }

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
        public string Gtin { get; set; }
        public float Weight { get; set; }
        public int Quantity { get; set; }

        public StockAlterationWithWeights(string gtin, int quantity, float weight)
        {
            this.Gtin = gtin;
            this.Quantity = quantity;
            this.Weight = weight;

            if (quantity < 0)
            {
                throw new MalformedRequestException("Alteration must be positive");
            }
        }
    }

    public class Truck
    {
        public int TruckNumber { get; set; }
        public float WeightInTruck { get; set; }
        public List<ProductInTruck> ProductsList { get; set; } = [];

        public override String ToString()
        {
            return new StringBuilder()
                .AppendFormat("TruckNumber: {0}, ", TruckNumber)
                .AppendFormat("WeightInTruck: {0}", WeightInTruck)
                .ToString();
        }
    }

    public class ProductInTruck
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public float TotalWeight { get; set; }

        public override String ToString()
        {
            return new StringBuilder()
                .AppendFormat("ProductId: {0}, ", ProductId)
                .AppendFormat("\t ProductName: {0}", ProductName)
                .AppendFormat("\t Quantity: {0}", Quantity)
                .AppendFormat("\t TotalWeight: {0}", TotalWeight)
                .ToString();
        }
    }
}

//product:800kgs; 7000kg, 4
/*
    {truckNumber1:2000-1500, truckNumber2:2000-1500, truckNumber3:2000-1500, truckNumber4:2000-1500}
    */
/*
Truck : 1
WeightInTruck : 1800Kgs


Truck : 2
WeightInTruck : 1900Kgs


Truck : 3
WeightInTruck : 1500Kgs var minDiffer = 2000; 2000-WightInTruck< minDiffer, minDiffer = 2000-WightInTruck,

Truck : 4
WeightInTruck : 1600Kgs
*/
