﻿﻿using ShipIt.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ShipIt.Models.ApiModels
{
    public class StockAlteration
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        public StockAlteration(int productId, int quantity)
        {
            this.ProductId = productId;
            this.Quantity = quantity;

            if (quantity < 0)
            {
                throw new MalformedRequestException("Alteration must be positive");
            }
        }
    }
}

