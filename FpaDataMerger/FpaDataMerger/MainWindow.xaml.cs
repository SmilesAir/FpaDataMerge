using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
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

namespace FpaDataMerger
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;
		public string dataPath = "C:\\Github\\FpaDataMerge\\data";
		public List<FpaPlayerData> fpaPlayerDatas = new List<FpaPlayerData>();
		public List<RyanPlayerData> ryanPlayerDatas = new List<RyanPlayerData>();
		public List<PlayerCompareResult> compares = new List<PlayerCompareResult>();
		public List<PlayerMapping> exacts = new List<PlayerMapping>();
		public List<PlayerMapping> manuals = new List<PlayerMapping>();
		public List<FpaPlayerData> ignores = new List<FpaPlayerData>();
		public List<FpaPlayerData> omits = new List<FpaPlayerData>();
		ObservableCollection<string> leftOutput = new ObservableCollection<string>();
		public ObservableCollection<string> LeftOutput
		{
			get
			{
				return leftOutput;
			}
			set
			{
				leftOutput = value;
				OnPropertyChanged();
			}
		}
		ObservableCollection<string> rightOutput = new ObservableCollection<string>();
		public ObservableCollection<string> RightOutput
		{
			get
			{
				return rightOutput;
			}
		}

		public MainWindow()
		{
			InitializeComponent();
			this.DataContext = this;
		}

		protected void OnPropertyChanged([CallerMemberName] string name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		private bool caselessStringCompare(string str1, string str2)
		{
			return str1.Replace("\"", "").ToLower() == str2.Replace("\"", "").ToLower();
		}

		private bool comparePlayerName(FpaPlayerData fpa, RyanPlayerData ryan)
		{
			return caselessStringCompare(fpa.firstName, ryan.firstName) && caselessStringCompare(fpa.lastName, ryan.lastName);
		}

		public int CloseStringCompare(string s, string t)
		{
			int n = s.Length;
			int m = t.Length;
			int[,] d = new int[n + 1, m + 1];

			// Step 1
			if (n == 0)
			{
				return m;
			}

			if (m == 0)
			{
				return n;
			}

			// Step 2
			for (int i = 0; i <= n; d[i, 0] = i++)
			{
			}

			for (int j = 0; j <= m; d[0, j] = j++)
			{
			}

			// Step 3
			for (int i = 1; i <= n; i++)
			{
				//Step 4
				for (int j = 1; j <= m; j++)
				{
					// Step 5
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;

					// Step 6
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}
			// Step 7
			return d[n, m];
		}

		public int CloseStringCompare(FpaPlayerData fpa, RyanPlayerData ryan)
		{
			int a = CloseStringCompare(fpa.firstName, ryan.firstName) +
				CloseStringCompare(fpa.lastName, ryan.lastName);
			int b = CloseStringCompare(fpa.firstName, ryan.lastName) +
				CloseStringCompare(fpa.lastName, ryan.firstName);
			return a < b ? a : b;
		}

		public bool IsEvaluated(FpaPlayerData fpa)
		{
			return exacts.Exists(x => x.fpa?.id == fpa.id) ||
				manuals.Exists(x => x.fpa?.id == fpa.id) ||
				ignores.Exists(x => x.id == fpa.id) ||
				omits.Exists(x => x.id == fpa.id);
		}

		public int IsEvaluatedSorter(FpaPlayerData a, FpaPlayerData b)
		{
			bool isA = IsEvaluated(a);
			bool isB = IsEvaluated(b);

			if (isA && !isB)
			{
				return 1;
			}
			else if (!isA && isB)
			{
				return -1;
			}

			return 0;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			using (StreamReader sr = new StreamReader(dataPath + "\\fpaSqlPlayers.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					fpaPlayerDatas.Add(new FpaPlayerData(parts[2], parts[3], parts[0], parts[4]));
				}
			}

			using (StreamReader sr = new StreamReader(dataPath + "\\ryanPlayers.csv", UTF8Encoding.UTF8))
			{
				string? line = null;
				while ((line = sr.ReadLine()) != null)
				{
					string[] parts = line.Split(',');
					ryanPlayerDatas.Add(new RyanPlayerData(parts[1], parts[2], parts[0], parts[3]));
				}
			}

			LoadPlayerMappings();

			exacts.Clear();
			List<FpaPlayerData> remaining = new List<FpaPlayerData>();
			foreach (var fpaPlayer in fpaPlayerDatas)
			{
				bool found = false;
				List<MatchResult> bestRyans = new List<MatchResult>();
				foreach (var ryanPlayer in ryanPlayerDatas)
				{
					if (comparePlayerName(fpaPlayer, ryanPlayer))
					{
						exacts.Add(new PlayerMapping(fpaPlayer, ryanPlayer));
						found = true;
						break;
					}
					else
					{
						int score = CloseStringCompare(fpaPlayer, ryanPlayer);
						bestRyans.Add(new MatchResult(ryanPlayer, score));
						const int maxMatches = 3;
						if (bestRyans.Count > maxMatches)
						{
							bestRyans.Sort((a, b) => a.score - b.score);
							bestRyans.RemoveAt(maxMatches - 1);
						}
					}
				}

				if (!found)
				{
					remaining.Add(fpaPlayer);
					compares.Add(new PlayerCompareResult(fpaPlayer, bestRyans));
				}
			}

			//using (StreamWriter sw = new StreamWriter(dataPath + "\\fullPlayerMapping.csv"))
			//{
			//	foreach (var p in exacts)
			//	{
			//		sw.WriteLine(p.fpa.id + "," + p.ryan.id);
			//	}

			//	foreach (var p in manuals)
			//	{
			//		sw.WriteLine(p.fpa.id + "," + p.ryan.id);
			//	}
			//}

			compares.Sort((a, b) =>
		{
			int evaluatedCompare = IsEvaluatedSorter(a.fpa, b.fpa);
			if (evaluatedCompare != 0)
			{
				return evaluatedCompare;
			}

			if (a.matches[0].score <= 3 || b.matches[0].score <= 3)
			{
				return a.matches[0].score - b.matches[0].score;
			}

			return string.Compare(a.fpa.fullName, b.fpa.fullName);
		});

			foreach (PlayerCompareResult compare in compares)
			{
				string outStr = compare.fpa?.fullName ?? "Missing Name";
				if (exacts.Exists(x => x.fpa?.id == compare.fpa?.id))
				{
					outStr = "e - " + outStr;
				}
				else if (manuals.Exists(x => x.fpa?.id == compare.fpa?.id))
				{
					outStr = "m - " + outStr;
				}
				else if (ignores.Exists(x => x?.id == compare.fpa?.id))
				{
					outStr = "i - " + outStr;
				}
				else if (omits.Exists(x => x?.id == compare.fpa?.id))
				{
					outStr = "o - " + outStr;
				}

				leftOutput.Add(outStr);
			}

			//SavePlayerMappings();
		}

		public void LoadPlayerMappings()
		{
			manuals.Clear();

			// Only load manual/ignore/omits mappings. Exact mappings are calculated each time
			using (StreamReader sr = new StreamReader(dataPath + "\\playerMapping.txt"))
			{
				string? line = null;
				bool inManualSection = false;
				bool isIgnoreSection = false;
				bool isOmitSection = false;
				while ((line = sr.ReadLine()) != null)
				{
					if (line == "!!!manual")
					{
						inManualSection = true;
						isIgnoreSection = false;
						isOmitSection = false;
					}
					else if (line == "!!!ignore")
					{
						isIgnoreSection = true;
						inManualSection = false;
						isOmitSection = false;
					}
					else if (line == "!!!omit")
					{
						isOmitSection = true;
						isIgnoreSection = false;
						inManualSection = false;
					}
					else if (inManualSection)
					{
						string[] parts = line.Split(',');
						if (parts.Length >= 2)
						{
							manuals.Add(new PlayerMapping(fpaPlayerDatas.First(x => x.id == parts[0]),
								ryanPlayerDatas.First(x => x.id == parts[1])));
						}
					}
					else if (isIgnoreSection)
					{
						string[] parts = line.Split(',');
						if (parts.Length >= 1)
						{
							ignores.Add(fpaPlayerDatas.First(x => x.id == parts[0]));
						}
					}
					else if (isOmitSection)
					{
						string[] parts = line.Split(',');
						if (parts.Length >= 1)
						{
							omits.Add(fpaPlayerDatas.First(x => x.id == parts[0]));
						}
					}
				}
			}
		}

		public void SavePlayerMappings()
		{
			using (StreamWriter sw = new StreamWriter(dataPath + "\\playerMapping.txt"))
			{
				sw.WriteLine("!!!exact");
				foreach (PlayerMapping exact in exacts)
				{
					sw.WriteLine(exact.fpa?.id + "," + exact.ryan?.id + "," + exact.fpa?.fullName);
				}

				sw.WriteLine("!!!manual");
				foreach (PlayerMapping manual in manuals)
				{
					sw.WriteLine(manual.fpa?.id + "," + manual.ryan?.id + "," + manual.fpa?.fullName + "->" + manual.ryan?.fullName);
				}

				sw.WriteLine("!!!ignore");
				foreach (FpaPlayerData ignore in ignores)
				{
					sw.WriteLine(ignore.id + "," + ignore.fullName);
				}

				sw.WriteLine("!!!omit");
				foreach (FpaPlayerData omit in omits)
				{
					sw.WriteLine(omit.id + "," + omit.fullName);
				}
			}

			using (StreamWriter sw = new StreamWriter(dataPath + "\\playerMapping.json"))
			{
				sw.WriteLine("{");

				sw.WriteLine("\t\"exacts\":[");
				List<string> exactLines = new List<string>();
				foreach (PlayerMapping exact in exacts)
				{
					exactLines.Add($"\t\t{{\"fpaId\":\"{exact.fpa.id}\",\"ryanId\":\"{exact.ryan.id}\"}}");
				}
				sw.WriteLine(String.Join(",\r\n", exactLines));
				sw.WriteLine("\t],");

				sw.WriteLine("\t\"manuals\":[");
				List<string> manualLines = new List<string>();
				foreach (PlayerMapping manual in manuals)
				{
					manualLines.Add($"\t\t{{\"fpaId\":\"{manual.fpa.id}\",\"ryanId\":\"{manual.ryan.id}\"}}");
				}
				sw.WriteLine(String.Join(",\r\n", manualLines));
				sw.WriteLine("\t],");

				sw.WriteLine("\t\"ignores\":[");
				List<string> ignoreLines = new List<string>();
				foreach (FpaPlayerData ignore in ignores)
				{
					ignoreLines.Add($"\t\t{{\"fpaId\":\"{ignore.id}\"}}");
				}
				sw.WriteLine(String.Join(",\r\n", ignoreLines));
				sw.WriteLine("\t],");

				sw.WriteLine("\t\"omits\":[");
				List<string> omitLines = new List<string>();
				foreach (FpaPlayerData omit in omits)
				{
					omitLines.Add($"\t\t{{\"fpaId\":\"{omit.id}\"}}");
				}
				sw.WriteLine(String.Join(",\r\n", omitLines));
				sw.WriteLine("\t]");

				sw.WriteLine("}");
			}
		}

		private void connectButton_Click(object sender, RoutedEventArgs e)
		{
			FpaPlayerData fpa = compares[leftListBox.SelectedIndex].fpa;
			RyanPlayerData ryan = compares[leftListBox.SelectedIndex].matches[rightListBox.SelectedIndex].ryan;

			manuals.Add(new PlayerMapping(fpa, ryan));

			SavePlayerMappings();

			int selectedIndex = leftListBox.SelectedIndex;
			LeftOutput[selectedIndex] = "m - " + LeftOutput[selectedIndex];
			leftListBox.SelectedIndex = selectedIndex + 1;
		}

		private void skipButton_Click(object sender, RoutedEventArgs e)
		{
			FpaPlayerData fpa = compares[leftListBox.SelectedIndex].fpa;

			ignores.Add(fpa);

			SavePlayerMappings();

			int selectedIndex = leftListBox.SelectedIndex;
			LeftOutput[selectedIndex] = "i - " + LeftOutput[selectedIndex];
			leftListBox.SelectedIndex = selectedIndex + 1;
		}

		private void leftListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			int index = leftListBox.SelectedIndex;
			if (index >= 0)
			{
				rightOutput.Clear();
				foreach (MatchResult match in compares[index].matches)
				{
					string output = match.ryan?.fullName ?? "Missing Name";
					output += " " + match.score;
					rightOutput.Add(output);
				}

				rightListBox.SelectedIndex = 0;
			}
		}

		private void rightListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{

		}

		private void omitButton_Click(object sender, RoutedEventArgs e)
		{
			FpaPlayerData fpa = compares[leftListBox.SelectedIndex].fpa;

			omits.Add(fpa);

			SavePlayerMappings();

			int selectedIndex = leftListBox.SelectedIndex;
			LeftOutput[selectedIndex] = "o - " + LeftOutput[selectedIndex];
			leftListBox.SelectedIndex = selectedIndex + 1;
		}
	}

	public class PlayerMapping
	{
		public FpaPlayerData? fpa;
		public RyanPlayerData? ryan;

		public PlayerMapping()
		{
		}

		public PlayerMapping(FpaPlayerData fpa, RyanPlayerData ryan)
		{
			this.fpa = fpa;
			this.ryan = ryan;
		}
	}

	public class MatchResult
	{
		public RyanPlayerData? ryan;
		public int score = -1;

		public MatchResult() { }

		public MatchResult(RyanPlayerData ryan, int score)
		{
			this.ryan = ryan;
			this.score = score;
		}
	}

	public class PlayerCompareResult
	{
		public FpaPlayerData? fpa;
		public List<MatchResult> matches = new List<MatchResult>();

		public PlayerCompareResult()
		{
		}

		public PlayerCompareResult(FpaPlayerData fpa, List<MatchResult> matches)
		{
			this.fpa = fpa;
			this.matches = matches;
		}
	}

	public class FpaPlayerData
	{
		public string firstName = "";
		public string lastName = "";
		public string id = "";
		public string gender = "";
		public string fullName { get { return firstName + " " + lastName; } }

		public FpaPlayerData()
		{
		}

		public FpaPlayerData(string firstName, string lastName, string id, string gender)
		{
			this.firstName = firstName;
			this.lastName = lastName;
			this.id = id;
			this.gender = gender;
		}
	}

	public class RyanPlayerData
	{
		public string firstName = "";
		public string lastName = "";
		public string id = "";
		public string gender = "";
		public string fullName { get { return firstName + " " + lastName; } }

		public RyanPlayerData()
		{
		}

		public RyanPlayerData(string firstName, string lastName, string id, string gender)
		{
			this.firstName = firstName;
			this.lastName = lastName;
			this.id = id;
			this.gender = gender;
		}
	}
}