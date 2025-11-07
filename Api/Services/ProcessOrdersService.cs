using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;
using Api.Models;

namespace Api.Services
{
    public class ProcessOrdersService : BackgroundService
    {
        private readonly ServiceBusClient _serviceBusClient;
        private readonly string _inputQueueName;
        private readonly string _outputQueueName;

        public ProcessOrdersService(ServiceBusClient serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
            _inputQueueName = "orderrequests";
            _outputQueueName = "readytoship";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("OrderProcessing Service Started");

            var processor = _serviceBusClient.CreateProcessor(_inputQueueName);

            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler;

            await processor.StartProcessingAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(1000, stoppingToken);
            }

            await processor.StopProcessingAsync(stoppingToken);
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string orderRequest = args.Message.Body.ToString();

            OrderRequest order = JsonConvert.DeserializeObject<OrderRequest>(orderRequest);

            Console.WriteLine($"Info: OrderHandler => Processing the order for {order.productname}");
            order.status = OrderStatus.COMPLETED;

            await SendMessageToQueue(JsonConvert.SerializeObject(order));

            await args.CompleteMessageAsync(args.Message);
        }

        private async Task SendMessageToQueue(string message)
        {
            var sender = _serviceBusClient.CreateSender(_outputQueueName);
            var serviceBusMessage = new ServiceBusMessage(message);
            await sender.SendMessageAsync(serviceBusMessage);
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            Console.WriteLine($"Error: {args.Exception.Message}");
            return Task.CompletedTask;
        }
    }
}