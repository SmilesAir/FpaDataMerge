using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EventMerger
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;
		public string dataPath = "C:\\Github\\FpaDataMerge\\data";
		public List<FpaEventData> fpaEventDatas = new List<FpaEventData>();
		public List<RyanEventData> ryanEventDatas = new List<RyanEventData>();
		public List<EventData> events = new List<EventData>();
		public MainWindow()
		{
			InitializeComponent();
		}

		protected void OnPropertyChanged([CallerMemberName] string name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			using (StreamReader sr = new StreamReader(dataPath + "\\fpaSqlEvents.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					fpaEventDatas.Add(new FpaEventData(parts[0], parts[3], parts[1], parts[2], parts[4]));
				}
			}

			fpaEventDatas.Sort((a, b) => a.start.CompareTo(b.start));

			using (StreamReader sr = new StreamReader(dataPath + "\\ryanEvents.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					ryanEventDatas.Add(new RyanEventData(parts[0], parts[3], parts[1], parts[2]));
				}
			}

			ryanEventDatas.Sort((a, b) => a.start.CompareTo(b.start));

			List<string> allowEventIds = new List<string>()
			{
				"1692",
				"1685",
				"1773",
				"1834",
				"1892",
				"1897",
				"1921",
				"1938",
				"1951",
				"2062",
				"2064",
				"2083"
			};
			List<FpaEventData> fpaEventImports = new List<FpaEventData>();

			foreach (FpaEventData fpa in fpaEventDatas)
			{
				if (allowEventIds.Contains(fpa.id))
				{
					fpaEventImports.Add(fpa);
				}
				else
				{
					bool found = false;
					foreach (RyanEventData ryan in ryanEventDatas)
					{
						if (ryan.name.ToLower().Contains("test"))
						{
							continue;
						}

						int days = (fpa.start - ryan.start).Days;
						if (Math.Abs(days) < 2)
						{
							found = true;
							//Debug.WriteLine(fpa.name + " |<>| " + ryan.name + " ||| " + fpa.id);
						}
					}

					if (!found)
					{
						fpaEventImports.Add(fpa);
					}
				}
			}

			List<FpaResult> results = new List<FpaResult>();
			using (StreamReader sr = new StreamReader(dataPath + "\\fpaSqlResults.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					results.Add(new FpaResult(parts[1], parts[2], parts[3], parts[4], parts[5], int.Parse(parts[6]), parts[7]));
				}
			}

			// Find event result collisions
			HashSet<string> resultEventIds = new HashSet<string>();
			foreach (FpaResult result in results)
			{
				resultEventIds.Add(result.eventId);
			}

			Dictionary<string, string> eventHashes = new Dictionary<string, string>();
			foreach (string resultEventId in resultEventIds)
			{
				List<FpaResult> rows = results.FindAll(x => x.eventId == resultEventId);
				if (rows.Count > 0)
				{
					List<string> rowStrs = new List<string>();
					foreach (FpaResult row in rows)
					{
						string rowStr = $"{row.rank}-{row.playerId}";
						rowStrs.Add(rowStr);
					}

					rowStrs.Sort((a, b) => String.Compare(a, b));

					string hash = String.Join("|", rowStrs.ToArray());
					if (eventHashes.ContainsKey(hash))
					{
						Debug.WriteLine("Collision " + resultEventId + " = " + eventHashes[hash] + "   " + hash);
					}
					else
					{
						eventHashes.Add(hash, resultEventId);
					}
				}
			}

			HashSet<string> badHashes = new HashSet<string>()
			{
				"1-533|1-693",
				"1-3|1-899|2-900|2-901|3-1|3-902",
				"1-521|1-884"
			};

			Dictionary<string, string> processedWebpageIds = new Dictionary<string, string>();
			using (StreamReader sr = new StreamReader(dataPath + "\\webpageHashes.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					processedWebpageIds.Add(parts[0], parts[1]);
				}
			}

			//DownloadWebpageHashes(fpaEventImports, ref processedWebpageIds);

			Dictionary<string, string> eventToResultsId = new Dictionary<string, string>();
			foreach (FpaEventData fpa in fpaEventImports)
			{
				if (processedWebpageIds.ContainsKey(fpa.id))
				{
					string webpageHash = processedWebpageIds[fpa.id];
					//if (badHashes.Contains(webpageHash))
					//{
					//	Debug.WriteLine("Bad Hash: " + fpa.id);
					//	continue;
					//}

					string resultsId = eventHashes[webpageHash];
					if (resultsId != null)
					{
						eventToResultsId.Add(fpa.id, resultsId);
					}
				}
			}

			Dictionary<FpaEventData, List<FpaResult>> eventResults = new Dictionary<FpaEventData, List<FpaResult>>();
			foreach (var idMapping in eventToResultsId)
			{
				FpaEventData data = fpaEventDatas.First(x => x.id == idMapping.Key);
				List<FpaResult> res = results.FindAll(x => x.eventId == idMapping.Value);

				eventResults.Add(data, res);
			}

			List<EventData> filledEvents = new List<EventData>();
			foreach (var eventRes in eventResults)
			{
				EventData newEvent = new EventData();
				newEvent.fpa = eventRes.Key;

				Dictionary<string, List<FpaResult>> byDivision = new Dictionary<string, List<FpaResult>>();
				foreach (FpaResult res in eventRes.Value)
				{
					if (byDivision.ContainsKey(res.division))
					{
						byDivision[res.division].Add(res);
					}
					else
					{
						byDivision[res.division] = new List<FpaResult>();
						byDivision[res.division].Add(res);
					}
				}

				foreach (var divRes in byDivision)
				{
					newEvent.divisions.Add(ProcessDivision(divRes.Key, divRes.Value));
				}

				filledEvents.Add(newEvent);
			}

			Dictionary<string, string> playerMapping = new Dictionary<string, string>();
			using (StreamReader sr = new StreamReader(dataPath + "\\fullPlayerMapping.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					if (!playerMapping.ContainsKey(parts[0]))
					{
						playerMapping.Add(parts[0], parts[1]);
					}
				}
			}

			Dictionary<string, string> eventIdMapping = new Dictionary<string, string>();
			using (StreamReader sr = new StreamReader(dataPath + "\\eventIdMapping.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					eventIdMapping.Add(parts[0], parts[1]);
				}
			}

			//HashSet<string> mappedIds = new HashSet<string>();
			//foreach (FpaResult res in results)
			//{
			//	if (!playerMapping.ContainsKey(res.playerId))
			//	{
			//		if (!mappedIds.Contains(res.playerId))
			//		{
			//			mappedIds.Add(res.playerId);
			//		}
			//	}
			//}

			List<string> printNames = new List<string>()
			{
				//"fpaw",
				//"world champ",
				//"frisbeer"
			};

			List<Tuple<string, string, string, string>> markups = new List<Tuple<string, string, string, string>>();
			foreach (var fill in filledEvents)
			{
				bool shouldPrint = false;
				foreach (var printName in printNames)
				{
					if (fill.fpa.name.ToLower().Contains(printName))
					{
						shouldPrint = true;
						break;
					}
				}

				//if (!fill.fpa.name.Contains("1995 FPA World Championships"))
				//{
				//	continue;
				//}

				foreach (var div in fill.divisions)
				{
					//if (div.name != "00")
					//{
					//	continue;
					//}

					using (StringWriter sw = new StringWriter())
					{
						string divName = TranslateDivisionName(div.name);
						sw.WriteLine($"start pools {eventIdMapping[fill.fpa.id]} \"{divName}\"");

						foreach (var round in div.rounds)
						{
							sw.WriteLine($"round {TranslateRoundName(round.name)}");

							foreach (var pool in round.pools)
							{
								sw.WriteLine($"pool {TranslatePoolName(pool.name)}");

								foreach (var team in pool.teams)
								{
									string ids = GetRyanPlayerIds(playerMapping, team.playerIds);
									if (ids.Length > 0)
									{
										sw.WriteLine($"{team.rank} {ids} {team.score}");
									}
								}
							}
						}

						sw.WriteLine("end");

						string markup = sw.ToString();
						markups.Add(new Tuple<string, string, string, string>(fill.fpa.id, divName, fill.fpa.name.Replace("\"", ""), markup));

						if (shouldPrint)
						{
							Debug.WriteLine("");
							Debug.WriteLine(markup);
						}
					}
				}
			}

			using (StreamWriter sw = new StreamWriter(dataPath + "\\newResults.json"))
			{
				List<string> lines = new List<string>();
				sw.WriteLine("{");
				sw.WriteLine("\"results\": [");
				foreach (var markup in markups)
				{
					lines.Add($"{{\"id\": \"{eventIdMapping[markup.Item1]}\", \"division\": \"{markup.Item2}\", \"eventName\": \"{markup.Item3}\", \"input\": \"{Uri.EscapeDataString(markup.Item4)}\"}}");
				}
				sw.Write(String.Join(",\r\n", lines.ToArray()));
				sw.WriteLine("]");
				sw.WriteLine("}");
			}

			//using (StreamWriter sw = new StreamWriter(dataPath + "\\newEventSummaries.json"))
			//{
			//	List<string> lines = new List<string>();
			//	sw.WriteLine("{");
			//	sw.WriteLine("\"events\": [");
			//	foreach (var ev in filledEvents)
			//	{
			//		lines.Add($"{{\"id\": \"{ev.fpa.id}\", \"name\": \"{ev.fpa.name.Replace("\"", "")}\", \"start\": \"{ev.fpa.start}\", \"end\": \"{ev.fpa.end}\", \"postName\": \"{ev.fpa.postName}\", \"ryanId\": \"{eventIdMapping[ev.fpa.id]}\"}}");
			//	}
			//	sw.Write(String.Join(",\r\n", lines.ToArray()));
			//	sw.WriteLine("]");
			//	sw.WriteLine("}");
			//}

			Debug.WriteLine("hey ");
		}

		string GetRyanPlayerIds(Dictionary<string, string> playerMapping, List<string> fpaIds)
		{
			List<string> ryanIds = new List<string>();
			foreach (var id in fpaIds)
			{
				if (playerMapping.ContainsKey(id))
				{
					ryanIds.Add(playerMapping[id]);
				}
			}

			return String.Join(" ", ryanIds.ToArray());
		}

		string TranslateDivisionName(string div)
		{
			switch (div)
			{
				case "00":
					return "Open Pairs";
				case "01":
					return "Women Pairs";
				case "02":
					return "Open Co-op";
				case "03":
					return "Mixed Pairs";
				default:
					return "Open Pairs";
			}
		}

		string TranslateRoundName(string round)
		{
			return (int.Parse(round) + 1).ToString();
		}

		string TranslatePoolName(string pool)
		{
			switch (pool)
			{
				case "":
				case "1":
					return "A";
				case "2":
					return "B";
				case "3":
					return "C";
				default:
					return pool;
			}
		}

		DivisionData ProcessDivision(string name, List<FpaResult> results)
		{
			DivisionData div = new DivisionData();
			div.name = name;

			Dictionary<string, List<FpaResult>> byRound = new Dictionary<string, List<FpaResult>>();
			foreach (FpaResult res in results)
			{
				if (byRound.ContainsKey(res.round))
				{
					byRound[res.round].Add(res);
				}
				else
				{
					byRound[res.round] = new List<FpaResult>();
					byRound[res.round].Add(res);
				}
			}

			foreach (var roundRes in byRound)
			{
				div.rounds.Add(ProcessRound(roundRes.Key, roundRes.Value));
			}

			return div;
		}

		RoundData ProcessRound(string name, List<FpaResult> results)
		{
			RoundData round = new RoundData();
			round.name = name;

			Dictionary<string, List<FpaResult>> byPool = new Dictionary<string, List<FpaResult>>();
			foreach (FpaResult res in results)
			{
				if (byPool.ContainsKey(res.pool))
				{
					byPool[res.pool].Add(res);
				}
				else
				{
					byPool[res.pool] = new List<FpaResult>();
					byPool[res.pool].Add(res);
				}
			}

			foreach (var poolRes in byPool)
			{
				round.pools.Add(ProcessPool(poolRes.Key, poolRes.Value));
			}

			return round;
		}

		PoolData ProcessPool(string name, List<FpaResult> results)
		{
			PoolData pool = new PoolData();
			pool.name = name;

			foreach (FpaResult res in results)
			{
				TeamData? team = pool.teams.FirstOrDefault(x => x.rank == res.rank);
				if (team == null)
				{
					TeamData newTeam = new TeamData();
					newTeam.rank = res.rank;
					newTeam.score = res.score;
					pool.teams.Add(newTeam);
					team = newTeam;
				}

				team.playerIds.Add(res.playerId);
			}

			pool.teams.Sort((a, b) => a.rank - b.rank);

			return pool;
		}

		void DownloadWebpageHashes(List<FpaEventData> fpaEventImports, ref Dictionary<string, string> processedWebpageIds)
		{
			List<string> webpageHashLines = new List<string>();
			using (StreamWriter sw = new StreamWriter(dataPath + "\\webpageHashes.csv"))
			{
				foreach (var hash in processedWebpageIds)
				{
					string hashLine = hash.Key + "," + hash.Value;
					sw.WriteLine(hashLine);
					webpageHashLines.Add(hashLine);
				}
			}

			foreach (FpaEventData fpa in fpaEventImports)
			{
				if (processedWebpageIds.ContainsKey(fpa.id))
				{
					continue;
				}

				try
				{
					string url = "https://www.freestyledisc.org/event/" + fpa.postName;
					string resp = new WebClient().DownloadString(url);

					string resultsLine = "";
					using (StringReader sr = new StringReader(resp))
					{
						string? line = null;
						while ((line = sr.ReadLine()) != null)
						{
							if (line.Contains("event-results-container"))
							{
								resultsLine = line;
								break;
							}
						}
					}

					if (resultsLine.Length == 0)
					{
						continue;
					}

					int start = 0;
					while (true)
					{
						int open = resultsLine.IndexOf("<tr", start);
						int nextOpen = resultsLine.IndexOf("<tr", open + 1);
						int close = resultsLine.IndexOf("</tr", start);

						if (open == -1 || nextOpen == -1)
						{
							break;
						}

						if (nextOpen < close)
						{
							resultsLine = resultsLine.Insert(nextOpen + 1, "/");
							start = 0;
						}
						else
						{
							start = close + 1;
						}
					}

					if (resultsLine.Length > 0)
					{
						resultsLine = resultsLine.Replace("&", "");
						resp = System.Xml.Linq.XElement.Parse(resultsLine).ToString();

						List<string> resultStrings = new List<string>();
						using (StringReader sr = new StringReader(resp))
						{
							string? line = null;
							int rank = -1;
							while ((line = sr.ReadLine()) != null)
							{
								line = line.Trim();
								if (line.Contains("<td class=\"finish\">"))
								{
									rank = int.Parse(line.Replace("<td class=\"finish\">", "").Replace("</td>", ""));
								}
								else if (line.Contains("<ul class=\"players\">"))
								{
									if (rank < 0)
									{
										Debug.WriteLine("error");
									}

									while ((line = sr.ReadLine()) != null)
									{
										if (line.Contains("<li id=\""))
										{
											line = line.Trim().Replace("<li id=\"", "");
											string result = $"{rank}-{line.Substring(0, line.IndexOf("\""))}";
											resultStrings.Add(result);
										}
										else if (line.Contains("</ul>"))
										{
											break;
										}
									}
								}
							}
						}
						resultStrings.Sort((a, b) => String.Compare(a, b));
						string webpageHash = String.Join("|", resultStrings.ToArray());
						webpageHashLines.Add(fpa.id + "," + webpageHash);

						using (StreamWriter sw = new StreamWriter(dataPath + "\\webpageHashes.csv"))
						{
							string output = String.Join("\r\n", webpageHashLines.ToArray());
							sw.WriteLine(output);
							Debug.WriteLine(output);
						}
					}
				}
				catch (WebException except)
				{
					Debug.WriteLine($"Web: {except.Message}");
				}
				catch (Exception ex)
				{
					Debug.WriteLine("Bad: " + ex.Message);
				}
			}
		}
	}

	public class FpaEventData
	{
		public string id = "";
		public string name = "";
		public DateTime start = DateTime.MinValue;
		public DateTime end = DateTime.MinValue;
		public string postName = "";

		public FpaEventData() { }

		public FpaEventData(string id, string name, string start, string end, string postName)
		{
			this.id = id;
			this.name = name;
			this.start = DateTime.Parse(start);
			this.end = DateTime.Parse(end);
			this.postName = postName;
		}
	}

	public class RyanEventData
	{
		public string id = "";
		public string name = "";
		public DateTime start = DateTime.MinValue;
		public DateTime end = DateTime.MinValue;

		public RyanEventData() { }

		public RyanEventData(string id, string name, string start, string end)
		{
			this.id = id;
			this.name = name;
			this.start = DateTime.Parse(start);
			this.end = DateTime.Parse(end);
		}
	}

	public class FpaResult
	{
		public string eventId = "";
		public string playerId = "";
		public string round = "";
		public string pool = "";
		public string division = "";
		public int rank = 0;
		public string score = "";

		public FpaResult() { }

		public FpaResult(string eventId, string playerId, string round, string pool, string division, int rank, string score)
		{
			this.eventId = eventId;
			this.playerId = playerId;
			this.round = round;
			this.pool = pool;
			this.division = division;
			this.rank = rank;
			this.score = score;
		}
	}

	public class TeamData
	{
		public List<string> playerIds = new List<string>();
		public int rank = 0;
		public string score = "";
	}
	public class PoolData
	{
		public string name = "";
		public List<TeamData> teams = new List<TeamData>();
	}

	public class RoundData
	{
		public string name = "";
		public List<PoolData> pools = new List<PoolData>();
	}

	public class DivisionData
	{
		public string name = "";
		public List<RoundData> rounds = new List<RoundData>();
	}

	public class EventData
	{
		public FpaEventData? fpa;
		public List<DivisionData> divisions = new List<DivisionData>();

		public EventData() { }

		public EventData(FpaEventData fpa)
		{
			this.fpa = fpa;
		}
	}

	public class PostData
	{
		public string id = "";
		public string name = "";

		public PostData() { }

		public PostData(string id, string name)
		{
			this.id = id;
			this.name = name;
		}
	}
}