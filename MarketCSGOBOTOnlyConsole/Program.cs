using BotCsgo.Controller;
using BotCsgo.Model;
using BotCsgo.Model.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MarketCSGOBOTOnlyConsole
{
	class Program
	{
		public static string answer;
		public static MarketApi api;
		static void Main(string[] args)
		{
			StartBot(15, 2);
			Console.WriteLine();
			answer = Console.ReadLine();
			Console.WriteLine("Бот стопанулся");
			StopSellOrders(api);
			Console.ReadLine();
		}

		public static async void StartBot(double ordermax, double freeze)
		{
			api = new MarketApi(KeyContorller.LoadKey());
			bool BotStart = false;
			bool ItemReceived = false;
			bool IsBotStop = true;

			while (IsBotStop)
			{
				await Task.Run(async () =>
				{
					try
					{

						int countGet = 0;
						var balance = await api.GetBalance();
						Thread.Sleep(1);
						var orders = await api.GetOrders();
						var changeOrders = 0;
						for (int i = 0; i < orders.Count; i++)
						{
							if (orders[i].Info == orders[i].O_price)
							{
								var item = await api.GetItemInfo(orders[i].I_classid, orders[i].I_instanceid);
								var prevBuyOffer = new buy_offers();

								for (int a = 0; a < item.Buy_offers.Length; a++)
								{
									if (a ==  1)
									{
										prevBuyOffer = item.Buy_offers[a];
										break;
									}
								}
								if ((Convert.ToInt32(orders[i].Info) - 1).ToString() == prevBuyOffer.O_price)
								{
									Console.WriteLine($"Скип из-за цена равно запросу {orders[i].I_market_hash_name}");
								}
								else
								{
									await api.UpdateOrder(orders[i].I_classid, orders[i].I_instanceid, (Convert.ToInt32(prevBuyOffer.O_price) + 1).ToString());
									countGet += 1;
									changeOrders++;
									Console.WriteLine($"{orders[i].I_market_hash_name} - Изменён прайс");
								}

								continue;
							}
							else
							{
								if (Convert.ToInt32(orders[i].Info) >= Convert.ToInt32(balance))
								{
									Console.WriteLine($"Пропуск {orders[i].I_market_hash_name}");
									await api.UpdateOrder(orders[i].I_classid, orders[i].I_instanceid, "1");
									continue;
								}
								else
								{
									if (countGet == 5)
									{
										countGet = 0;
									}
									var item = await api.GetItemInfo(orders[i].I_classid, orders[i].I_instanceid);
									if (item.Buy_offers != null)
									{
										var priceOffer = item.Offers.OrderBy(offer => offer.Price).First().Price;
										var price = Convert.ToInt32(priceOffer);
										var threshold = price * Convert.ToInt32(ordermax) / 100;
										var result = price - threshold;
										var maxBuyOffer = new buy_offers();

										foreach (var buy_Offer in item.Buy_offers)
										{
											maxBuyOffer = buy_Offer;

											break;
										}
										var newPrice = Convert.ToInt32(maxBuyOffer.O_price) + 1;
										if (result > newPrice)
										{
											await api.UpdateOrder(orders[i].I_classid, orders[i].I_instanceid, newPrice.ToString());
											countGet += 1;
											changeOrders++;
											Console.WriteLine($"{orders[i].I_market_hash_name} - Изменён прайс");
										}
									}
									else
									{
										Console.WriteLine($"{orders[i].I_market_hash_name} - ошибка");
										continue;
									}
								}
							}
						}
						Console.WriteLine($"Количество запросов на автопокупку: {orders.Count} из них были выполненны: {changeOrders}");
					}
					catch (Exception ex)
					{
						Console.WriteLine($"{ex.Message} {ex.StackTrace} {ex.InnerException} {ex.StackTrace} - ошибка");
						api.StartPing();
					}
					CheckStatusItemsAndAddOrder(api);
					Thread.Sleep(TimeSpan.FromSeconds(Convert.ToInt32(freeze)));
					if (answer == "stop")
					{
						IsBotStop = false;
					}
				});
			}
		}

		public static async void StopSellOrders(MarketApi api)
		{
			await Task.Run(async () =>
			{
				api = new MarketApi(KeyContorller.LoadKey());
				var orders = await api.GetOrders();
				for (int i = 0; i < orders.Count; i++)
				{
					await api.UpdateOrder(orders[i].I_classid, orders[i].I_instanceid, "1");
				}
			});
		}
		public static async void CheckStatusItemsAndAddOrder(MarketApi api)
		{
			bool BotStart = false;
			bool ItemReceived = false;
			bool ItemBuyed = false;
			items CurrentItemReceived = null;
			items CurrentItemBuyed = null;

			var items = await api.GetOrdersWithStatus();
			if (items != null)
			{
				Console.WriteLine("----Информация о инвентаре----");
				for (int i = 0; i < items.Count; i++)
				{
					switch (items[i].Status)
					{
						case "2":
							if (ItemBuyed == false && CurrentItemBuyed != items[i])
							{
								ItemBuyed = true;
								CurrentItemBuyed = items[i];
								Console.WriteLine($"{DateTime.Now}: Предмет {items[i].Market_hash_name} продан за {items[i].Price} \n");
								await api.ItemReqeustIn(items[i].Botid);
							}
							else
							{
								if (CurrentItemBuyed.Classid != items[i].Classid)
								{
									ItemBuyed = false;
									CurrentItemBuyed = null;
								}
								else
								{
									continue;
								}
							}
							continue;
						case "3":
							AddOrder(items[i], api);
							Console.WriteLine($"{DateTime.Now}: Предмет {items[i].Market_hash_name} добавлен ордер\n");
							continue;
						case "4":
							if (ItemReceived != true && CurrentItemReceived != items[i])
							{
								ItemReceived = true;
								CurrentItemReceived = items[i];
								Console.WriteLine($"{DateTime.Now}: Предмет {items[i].Market_hash_name} куплен за {items[i].Price} \n");
								AddOrder(items[i], api);
								var itemrequest = await api.ItemReqeustOut(items[i].Botid);
							}
							else
							{
								if (CurrentItemReceived != items[i])
								{
									ItemReceived = false;
									CurrentItemReceived = null;
								}
								else
								{
									continue;
								}
							}
							continue;
						default:
							break;
					}
				}
				Console.WriteLine("----конец----");
			}
		}

		public static async void AddOrder(items currentItems, MarketApi api)
		{
			try
			{
				await Task.Run(async () =>
				{
					var getItemInfoWithStatusThree = await api.GetItemInfo(currentItems.Classid, currentItems.Instanceid);
					api.AddOrdersForBot(getItemInfoWithStatusThree.Classid, getItemInfoWithStatusThree.Instanceid);
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Не удалось добавить ордер по предемету или ордер уже создан: {currentItems.Market_hash_name}");
			}
		}
	}
}
