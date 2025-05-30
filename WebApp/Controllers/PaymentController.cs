﻿using Microsoft.AspNetCore.Mvc;
using WebApp.ApiClients;
using WebApp.ViewModels;
using WebApp.DTOs;
using System.Text.Json;

namespace WebApp.Controllers
{
    public class PaymentController : Controller
    {
        private readonly PaymentApiClient _paymentApiClient;
        private readonly OrderApiClient _orderApiClient;

        public PaymentController(PaymentApiClient paymentApiClient, OrderApiClient orderApiClient)
        {
            _paymentApiClient = paymentApiClient;
            _orderApiClient = orderApiClient;
        }

        [HttpGet, HttpPost]
        public IActionResult Checkout()
        {
            var cart = GetCartFromTempData();
            TempData.Keep("Cart");
            return View(cart);
        }

        [HttpGet, HttpPost]
        public async Task<IActionResult> Process([FromForm] string method)
        {
            var cart = GetCartFromTempData();

            if (!cart.Any())
                return RedirectToAction("Index", "Cart");

            var orderDto = new OrderDto
            {
                OrderDate = DateTime.UtcNow,
                TableId = 2,
                Status = "Paid",
                TotalAmount = cart.Sum(x => x.Price * x.Quantity)
            };

            var createdOrder = await _orderApiClient.CreateOrder(orderDto);
            if (createdOrder == null) return View("Checkout"); 

            // kreiranje itemsa narudzbe
            var listOfitemDtos = cart.Select(c => new OrderItemDto
            {
                OrderId = createdOrder!.Id,
                ProductId = c.Id,
                ProductName = c.Name,
                UnitPrice = c.Price,
                Quantity = c.Quantity
            }).ToList();

            var itemsAddedToOrder = await _orderApiClient.AddOrderItemsToOrder(listOfitemDtos);

            var paymentDto = new PaymentDto
            {
                Amount = createdOrder.TotalAmount,
                Method = "CreditCard",
                PaymentDate = DateTime.UtcNow,
                OrderId = createdOrder.Id
            };
            
            var paymentResponse = await _paymentApiClient.CreatePaymentAsync(paymentDto);

            // ovo nam treba za cuvanje ID za tracking, ne brisati
            HttpContext.Session.SetInt32("LastOrderId", createdOrder.Id);

            return RedirectToAction("Success", new { id = createdOrder.Id, orderStatus = createdOrder.Status, table = orderDto.TableId });
        }

        public IActionResult Success(int id, string orderStatus, int table)
        {
            ViewBag.OrderId = id;
            ViewBag.OrderStatus = orderStatus;
            ViewBag.TableNumber = table;
            return View("Success");
        }
        private List<ProductCartViewModel> GetCartFromTempData()
        {
            return TempData.ContainsKey("Cart")
                ? JsonSerializer.Deserialize<List<ProductCartViewModel>>(TempData["Cart"]!.ToString()!) ?? new()
                : new();
        }
    }
}
