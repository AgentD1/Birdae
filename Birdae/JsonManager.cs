using System.Collections.Concurrent;
using Newtonsoft.Json;

namespace Birdae;

public static class JsonManager {
	public static void SerializeStateToFile() {
		List<AviaryJson> aviaries = new List<AviaryJson>();

		foreach (var guildAviaryList in Aviary.guildAndUserToAviaryDict) {
			foreach (var av in guildAviaryList.Value) {
				lock (av.Value) {
					aviaries.Add(AviaryJson.Serialize(av.Value));
				}
			}
		}

		List<User> users = User.guildAndIdToUser.SelectMany(g => g.Value.Values).ToList();

		SaveStateJson ssj = new SaveStateJson {
			aviaries = aviaries,
			settings = Settings.instance,
			users = users,
		};

		File.WriteAllText("birdae.json", JsonConvert.SerializeObject(ssj));

		File.Copy("birdae.json", $"birdae-{DateTime.Now.Ticks}.json");
	}

	public static void LoadStateFromFile() {
		SaveStateJson ssj = JsonConvert.DeserializeObject<SaveStateJson>(File.ReadAllText("birdae.json"));

		Settings.instance = ssj.settings;

		foreach (var aviaryJson in ssj.aviaries) {
			var guildAviaries = Aviary.guildAndUserToAviaryDict.GetOrAdd(aviaryJson.guildId, _ => new ConcurrentDictionary<ulong, Aviary>());

			guildAviaries.TryAdd(aviaryJson.userId, aviaryJson.Deserialize());
		}

		foreach (var aviaries in Aviary.guildAndUserToAviaryDict) {
			var available = Aviary.guildAvailableBirds.GetOrAdd(aviaries.Key, _ => new ConcurrentDictionary<Bird, byte>());

			foreach (Bird b in Program.birdDb.birdsById) {
				available.TryAdd(b, 0);
			}

			foreach (var aviary in aviaries.Value.Values) {
				foreach (Bird bird in aviary.birds) {
					available.TryRemove(bird, out _);
				}
			}
		}

		foreach (User u in ssj.users) {
			User.guildAndIdToUser.GetOrAdd(u.guildId, _ => new ConcurrentDictionary<ulong, User>()).TryAdd(u.id, u);
		}
	}

	public struct SaveStateJson {
		public List<AviaryJson> aviaries;
		public Settings settings;
		public List<User> users;
	}

	public struct AviaryJson {
		public string name;
		public ulong guildId, userId;
		public int[] birds;

		public Aviary Deserialize() {
			return new Aviary(userId, guildId, name) {
				birds = new List<Bird>(birds.Select(i => Program.birdDb.birdsById[i]))
			};
		}

		public static AviaryJson Serialize(Aviary a) {
			return new AviaryJson {
				name = a.name,
				guildId = a.guildId,
				userId = a.userId,
				birds = a.birds.Select(b => b.id).ToArray(),
			};
		}
	}
}