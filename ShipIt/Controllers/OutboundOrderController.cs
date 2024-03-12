﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.Hosting;
using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{
    [Route("orders/outbound")]
    public class OutboundOrderController : ControllerBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType
        );

        private readonly IStockRepository _stockRepository;
        private readonly IProductRepository _productRepository;

        public OutboundOrderController(
            IStockRepository stockRepository,
            IProductRepository productRepository
        )
        {
            _stockRepository = stockRepository;
            _productRepository = productRepository;
        }

        [HttpPost("")]
        public OutboundOrderResponseModel Post([FromBody] OutboundOrderRequestModel request)
        {
            Log.Info(String.Format("Processing outbound order: {0}", request));

            var gtins = new List<String>();
            foreach (var orderLine in request.OrderLines)
            {
                if (gtins.Contains(orderLine.gtin))
                {
                    throw new ValidationException(
                        String.Format(
                            "Outbound order request contains duplicate product gtin: {0}",
                            orderLine.gtin
                        )
                    );
                }
                gtins.Add(orderLine.gtin);
            }

            var productDataModels = _productRepository.GetProductsByGtin(gtins);
            var products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));

            var lineItems = new List<StockAlteration>();
            var productIds = new List<int>();
            var errors = new List<string>();
            var lineItemsWithWeights = new List<StockAlterationWithWeights>();

            foreach (var orderLine in request.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))
                {
                    errors.Add(string.Format("Unknown product gtin: {0}", orderLine.gtin));
                }
                else
                {
                    var product = products[orderLine.gtin];
                    lineItems.Add(new StockAlteration(product.Id, orderLine.quantity));
                    lineItemsWithWeights.Add(
                        new StockAlterationWithWeights(
                            product.Gtin,
                            orderLine.quantity,
                            product.Weight
                        )
                    );
                    productIds.Add(product.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new NoSuchEntityException(string.Join("; ", errors));
            }

            var stock = _stockRepository.GetStockByWarehouseAndProductIds(
                request.WarehouseId,
                productIds
            );

            var orderLines = request.OrderLines.ToList();
            errors = new List<string>();

            for (int i = 0; i < lineItems.Count; i++)
            {
                var lineItem = lineItems[i];
                var orderLine = orderLines[i];

                if (!stock.ContainsKey(lineItem.ProductId))
                {
                    errors.Add(string.Format("Product: {0}, no stock held", orderLine.gtin));
                    continue;
                }

                var item = stock[lineItem.ProductId];
                if (lineItem.Quantity > item.held)
                {
                    errors.Add(
                        string.Format(
                            "Product: {0}, stock held: {1}, stock to remove: {2}",
                            orderLine.gtin,
                            item.held,
                            lineItem.Quantity
                        )
                    );
                }
            }

            float totalOrderWeightInKgs = 0;
            int numberOfTrucks = 0;
            var trucks = new List<Truck>();

            //{product.id, toalWeight}

            if (errors.Count > 0)
            {
                throw new InsufficientStockException(string.Join("; ", errors));
            }
            // If there are no Errors in the Order then Load the Data into trucks
            else
            {
                var truckNum = 0;
                totalOrderWeightInKgs =
                    lineItemsWithWeights.Sum(item => item.Weight * item.Quantity) / 1000;
                numberOfTrucks = (int)Math.Ceiling(totalOrderWeightInKgs / 2000);
                Truck currentTruck = null;
                float MaxTruckWeight = 2000;
                //What to do if trucks has weight left?[1500, 1800, 500, 100]
                //What if ProductWeight>2000?
                foreach (var item in lineItemsWithWeights)
                {
                    var product = products[item.Gtin];
                    var weightInKgsPerProduct = product.Weight / 1000;
                    var quantity = item.Quantity;
                    do
                    {
                        //Following loop is to Chack if there is a truck that can accommodate current product
                        foreach (var truck in trucks)
                        {
                            if (MaxTruckWeight - truck.WeightInTruck > weightInKgsPerProduct)
                            {
                                currentTruck = truck;
                                break;
                            }
                            else
                            {
                                currentTruck = null;
                            }
                        }
                        //If theere is no truck that cannot accommodate the current product then
                        //load it into new truck
                        if (currentTruck == null)
                        {
                            currentTruck = new Truck
                            {
                                TruckNumber = ++truckNum,
                                WeightInTruck = 0,
                                ProductsList = new List<ProductInTruck>()
                            };
                            trucks.Add(currentTruck);
                        }
                        // Console.WriteLine($"**** Quantity {quantity}");
                        var productTotalWeight = (quantity * product.Weight) / 1000;

                        var WeightAvailable = MaxTruckWeight - currentTruck.WeightInTruck;
                        if (WeightAvailable >= productTotalWeight)
                        {
                            // Console.WriteLine($"Weight Available: {WeightAvailable}");
                            // Console.WriteLine($"---->{truckWeight}");
                            var currProduct = new ProductInTruck
                            {
                                ProductId = product.Id,
                                ProductName = product.Name,
                                Quantity = quantity,
                                TotalWeight = quantity * weightInKgsPerProduct,
                            };
                            currentTruck.WeightInTruck += productTotalWeight;
                            currentTruck.ProductsList.Add(currProduct);
                            quantity = 0;
                        }
                        else if (WeightAvailable >= weightInKgsPerProduct)
                        {
                            // Console.WriteLine($"---->WeightAvailable: {WeightAvailable}");
                            // Console.WriteLine(
                            //     $"---->weightInKgsPerProduct:{weightInKgsPerProduct}"
                            // );

                            //Following is to calculate how many products that can be fit in the available space in truck
                            var quantityFitInTruck = (int)
                                Math.Floor(WeightAvailable / weightInKgsPerProduct);
                            var quantityWeight = quantityFitInTruck * weightInKgsPerProduct;
                            var currProduct = new ProductInTruck
                            {
                                ProductId = product.Id,
                                ProductName = product.Name,
                                Quantity = quantityFitInTruck,
                                TotalWeight = quantityWeight,
                            };
                            quantity = quantity - quantityFitInTruck;
                            currentTruck.ProductsList.Add(currProduct);
                            currentTruck.WeightInTruck += quantityWeight;
                            WeightAvailable -= quantityWeight;
                            // Console.WriteLine($"QuantityThatFitInTruck: {quantityFitInTruck}");
                            // Console.WriteLine($"quantityWeight: {quantityWeight}");
                        }
                    } while (quantity > 0);
                }
            }

            _stockRepository.RemoveStock(request.WarehouseId, lineItems);
            return new OutboundOrderResponseModel()
            {
                TotalOrderWeightInKgs = totalOrderWeightInKgs,
                NumberOfTrucks = numberOfTrucks,
                Trucks = trucks
            };
        }
    }
}
