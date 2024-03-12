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
            else
            {
                //lineItemsWithWeights<Gtin,quantity and weight>
                // var TrucksLookup = new Dictionary<int, Truck>();

                var truckNum = 0;
                totalOrderWeightInKgs =
                    lineItemsWithWeights.Sum(item => item.Weight * item.Quantity) / 1000;
                numberOfTrucks = (int)Math.Ceiling(totalOrderWeightInKgs / 2000);

                float truckWeight = 0;
                var productsInTruck = new List<ProductInTruck>();

                //What to do if trucks has weight left?[1500, 1800, 500, 100]
                //What if ProductWeight>2000?
                //First Truck
                Truck currentTruck = new Truck
                {
                    TruckNumber = ++truckNum,
                    WeightInTruck = 0,
                    ProductsList = new List<ProductInTruck>()
                };
                float MaxTruckWeight = 2000;
                
                
                for (int i = 0; i < lineItemsWithWeights.Count; i++)
                {
                    var item = lineItemsWithWeights[i];
                    var product = products[item.Gtin];
                    var weightInKgsPerProduct = product.Weight / 1000;
                    var quantity = item.Quantity;
                    //truck=1800
                    //produc=400
                    do
                    {
                        
                        Console.WriteLine($"**** Quantity {quantity}");
                        var productTotalWeight = (quantity * product.Weight) / 1000;

                        if (productTotalWeight > MaxTruckWeight)
                        {
                            //DoSomething
                        }
                        // truckWeight = currentTruck.WeightInTruck + productTotalWeight;
                        var WeightAvailable = MaxTruckWeight - currentTruck.WeightInTruck;
                        if (WeightAvailable >= productTotalWeight)
                        {
                            Console.WriteLine($"Weight Available: {WeightAvailable}");
                            Console.WriteLine($"---->{truckWeight}");
                            var currProduct = new ProductInTruck
                            {
                                ProductId = product.Id,
                                ProductName = product.Name,
                                Quantity = quantity,
                                TotalWeight = quantity * weightInKgsPerProduct,
                            };
                            currentTruck.WeightInTruck += productTotalWeight;
                            currentTruck.ProductsList.Add(currProduct);
                            currentTruck.ProductsList.ForEach(product =>
                            {
                                Console.WriteLine(product);
                            });
                            quantity = 0;
                        }
                        else if (WeightAvailable >= weightInKgsPerProduct)
                        {
                            Console.WriteLine($"---->WeightAvailable: {WeightAvailable}");
                            Console.WriteLine(
                                $"---->weightInKgsPerProduct:{weightInKgsPerProduct}"
                            );

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
                            Console.WriteLine($"QuantityThatFitInTruck: {quantityFitInTruck}");
                            Console.WriteLine($"quantityWeight: {quantityWeight}");
                            
                        }
                        if (WeightAvailable < weightInKgsPerProduct || quantity==0)
                        {
                            Console.WriteLine("TruckAdded");
                            trucks.Add(currentTruck);
                            currentTruck = new Truck
                            {
                                TruckNumber = ++truckNum,
                                WeightInTruck = 0,
                                ProductsList = new List<ProductInTruck>()
                            };
                            truckWeight = 0;
                        }
                        
                    } while (quantity > 0);
                }
            }

            _stockRepository.RemoveStock(request.WarehouseId, lineItems);
            return new OutboundOrderResponseModel()
            {
                TotalOrderWeightInKgs = totalOrderWeightInKgs,
                NumberOfTrucks = numberOfTrucks,
                Truck = trucks
            };
        }
    }
}

// while (lineItemsWithWeights.Count() > 0)
// {
//     float truckWeight = 0;
//     var productsInTruck = new List<ProductInTruck>();
//     //If There are no trucks
//     if (trucks.Count == 0)
//     {
//         var newTruck = new Truck()
//         {
//             TruckNumber = truckNum,
//             WeightInTruck = 0,
//             ProductsList = new List<ProductInTruck>()
//         };

//         trucks.Add(newTruck);
//         TrucksLookup[truckNum]=newTruck;
//     }
//     foreach (var item in lineItemsWithWeights) //[1500, 1800, 500, 100]
//     {
//         var product = products[item.Gtin];
//         var productWeight = (item.Quantity * product.Weight) / 1000;
//         var currProduct=  new ProductInTruck
//                     {
//                         ProductId = product.Id,
//                         ProductName = product.Name,
//                         Quantity = item.Quantity,
//                         TotalWeight = productWeight,
//                     };
//         if (productWeight > 2000) {
//             var quantity=item.Quantity;
//             //Do Something
//          }
//          bool ProductAdded = false;
//          for(int i =0;i<trucks.Count&&!ProductAdded;i++){
//             var currTruck=trucks[i];
//             var weightLeft = 2000-currTruck.WeightInTruck;
//             if(weightLeft>=productWeight){
//                 currTruck.ProductsList.Add(currProduct);
//                 lineItemsWithWeights.Remove(item);

//             }
//          }
//     }
// }
//SELECT * FROM public.stock as s
//Join public.gtin as g ON s.p_id=g.p_id WHERE s.w_id=9 Order By g.m_g desc;

//POST : OutboundOrderRequestModel
// {
// "WarehouseId" : 9,
// "OrderLines" : [
//                     {
//                         "gtin" : "0008336884202",
//                         "quantity" : 40
//                     },

//                     {
//                         "gtin" : "0008338484202",
//                         "quantity" : 29
//                     },

//                     {
//                         "gtin" : "0008346019908",
//                         "quantity" :50
//                     },

//                     {
//                         "gtin" : "0008346036004",
//                         "quantity" : 35
//                     },
//                     {
//                         "gtin" : "0024617180320",
//                         "quantity" : 35
//                     },
//                     {
//                         "gtin" : "0079100216901",
//                         "quantity" : 28
//                     },
//                     {
//                         "gtin" : "0730521100209",
//                         "quantity" : 31
//                     }
// ]
// }


// {
// "WarehouseId" : 9,
// "OrderLines" : [
//                     {
//                         "gtin" : "0049022327474",
//                         "quantity" : 4
//                     },

//                     {
//                         "gtin" : "0052649777782",
//                         "quantity" : 9
//                     },

//                     {
//                         "gtin" : "0052742462608",
//                         "quantity" :2
//                     },

//                     {
//                         "gtin" : "0022000115706",
//                         "quantity" : 5
//                     },
//                     {
//                         "gtin" : "7896262301121",
//                         "quantity" : 3
//                     }

// ]
// }
