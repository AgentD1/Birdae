using System.Collections.Concurrent;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;

namespace Birdae;

public class Aviary {
	public string name;
	public ulong userId;
	public ulong guildId;

	public List<Bird> birds = new();

	public Aviary(ulong userId, ulong guildId, string name) {
		this.userId = userId;
		this.guildId = guildId;
		this.name = name;
	}

	public static ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, Aviary>> guildAndUserToAviaryDict = new();
	public static ConcurrentDictionary<ulong, ConcurrentDictionary<Bird, byte>> guildAvailableBirds = new(); // Concurrent hashset (byte ignored)

	public static Semaphore savingSemaphore = new(0, 100000);

	public static Embed CreateAviaryCommand(SocketUser user, ulong guildId, string name) {
		var guildDict = guildAndUserToAviaryDict.GetOrAdd(guildId, _ => {
			guildAvailableBirds.TryAdd(guildId, new ConcurrentDictionary<Bird, byte>(Program.birdDb.birdsById.Select(b => new KeyValuePair<Bird, byte>(b, 0))));
			return new ConcurrentDictionary<ulong, Aviary>();
		});

		bool successfullyAdded = guildDict.TryAdd(user.Id, new Aviary(user.Id, guildId, name));

		User.guildAndIdToUser.GetOrAdd(guildId, _ => new ConcurrentDictionary<ulong, User>()).TryAdd(user.Id, new User(user.Id, guildId));

		if (!successfullyAdded) {
			return new EmbedBuilder()
				.WithTitle("You already have an aviary!")
				.WithDescription("If you really want to create a new one, " +
				                 "do /aviary delete. If you want to rename " +
				                 "your aviary, do /aviary rename [name].")
				.Build();
		}

		return new EmbedBuilder().WithTitle($"Your aviary {name} has been created!").Build();

		//return DisplayAviaryCommand(user, guildId, personal: true);
	}

	public static async Task DisplayAviaryCommand(SocketSlashCommand command) {
		SocketUser user = (SocketUser?)command.Data.Options.First().Options.FirstOrDefault()?.Value ?? command.User;
		bool personal = user == command.User;

		Aviary? userAviary = guildAndUserToAviaryDict.GetValueOrDefault(command.GuildId ?? throw new Exception("Empty Guild ID"))?.GetValueOrDefault(command.User.Id);

		if (userAviary == null) {
			if (personal) {
				await command.RespondAsync("You do not have an aviary! Create one with /aviary create [name]");
			} else {
				await command.RespondAsync("This user does not have an aviary!");
			}

			return;
		}

		EmbedBuilder builder = new EmbedBuilder {
			Title = userAviary.name,
			Description = $"{(personal ? "Your" : $"{user.Username}'s")} Aviary",
			Fields = new List<EmbedFieldBuilder>(),
		};

		lock (userAviary) {
			Bird.AppendBirdList(builder, userAviary.birds);
		}

		await Paginator.SendPagination(command, builder);
	}

	public static bool AddBirdToAviary(SocketUser user, ulong guildId, Bird bird) {
		var userAviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(user.Id);

		if (userAviary == null) return false;

		lock (userAviary)
		lock (guildAvailableBirds[guildId]) {
			if (!guildAvailableBirds[guildId].ContainsKey(bird)) return false;

			userAviary.birds.Add(bird);
			guildAvailableBirds[guildId].TryRemove(bird, out _);
			return true;
		}
	}

	public static bool RemoveBirdFromAviary(SocketUser user, ulong guildId, Bird bird) {
		var userAviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(user.Id);

		if (userAviary == null) return false;

		lock (userAviary)
		lock (guildAvailableBirds[guildId]) {
			if (!userAviary.birds.Remove(bird)) return false;

			guildAvailableBirds[guildId].TryRemove(bird, out _);
			return true;
		}
	}

	public static Embed GiveBird(SocketUser giver, SocketUser receiver, ulong guildId, Bird bird) {
		// TODO: Make receiver accept the gift
		Aviary? giverAviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(giver.Id);
		Aviary? receiverAviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(receiver.Id);

		if (giverAviary == null) return new EmbedBuilder().WithTitle($"{giver.Mention} does not have an aviary!").Build();
		if (receiverAviary == null) return new EmbedBuilder().WithTitle($"{receiver.Mention} does not have an aviary!").Build();

		Aviary lowestLock = giver.Id < receiver.Id ? giverAviary : receiverAviary;
		Aviary highestLock = giver.Id > receiver.Id ? giverAviary : receiverAviary;

		lock (lowestLock)
		lock (highestLock) {
			if (!giverAviary.birds.Contains(bird)) {
				return new EmbedBuilder().WithTitle("You do not have this bird!").Build();
			}

			giverAviary.birds.Remove(bird);
			receiverAviary.birds.Add(bird);

			return new EmbedBuilder().WithTitle($"Your {bird.name} has been transferred to {receiver.Username}!").Build();
		}
	}

	public static Embed TradeBirds(SocketUser user1, SocketUser user2, ulong guildId, Bird[] user1Gives, Bird[] user2Gives) {
		// TODO: test this
		Aviary? user1Aviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(user1.Id);
		Aviary? user2Aviary = guildAndUserToAviaryDict.GetValueOrDefault(guildId)?.GetValueOrDefault(user1.Id);

		if (user1Aviary == null) return new EmbedBuilder().WithTitle($"{user1.Mention} does not have an aviary!").Build();
		if (user2Aviary == null) return new EmbedBuilder().WithTitle($"{user2.Mention} does not have an aviary!").Build();

		Aviary lowestLock = user1.Id < user2.Id ? user1Aviary : user2Aviary;
		Aviary highestLock = user1.Id > user2.Id ? user1Aviary : user2Aviary;

		lock (lowestLock)
		lock (highestLock) {
			if (user1Aviary.birds.Intersect(user1Gives).Count() != user1Aviary.birds.Count) {
				return new EmbedBuilder().WithTitle($"{user1.Mention} does not have all the birds in the trade!").Build();
			}

			if (user2Aviary.birds.Intersect(user2Gives).Count() != user2Aviary.birds.Count) {
				return new EmbedBuilder().WithTitle($"{user2.Mention} does not have all the birds in the trade!").Build();
			}

			foreach (var bird in user1Gives) {
				user1Aviary.birds.Remove(bird);
				user2Aviary.birds.Add(bird);
			}

			foreach (var bird in user2Gives) {
				user2Aviary.birds.Remove(bird);
				user1Aviary.birds.Add(bird);
			}
		}

		EmbedBuilder b = new EmbedBuilder {
			Title = "Trade complete!"
		};

		b.Fields = new List<EmbedFieldBuilder> {
			new EmbedFieldBuilder().WithName(user1.Mention).WithValue($"{user1Gives.Length} birds given").WithIsInline(false),
			new EmbedFieldBuilder().WithName(user2.Mention).WithValue($"{user2Gives.Length} birds given").WithIsInline(true),
		};

		for (int i = 0; i < Math.Max(user1Gives.Length, user2Gives.Length); i++) {
			if (user1Gives.Length < i) {
				b.Fields.Add(new EmbedFieldBuilder().WithName(user1Gives[i].name).WithIsInline(false));
			} else {
				b.Fields.Add(new EmbedFieldBuilder().WithName("-").WithIsInline(false));
			}

			if (user2Gives.Length < i) {
				b.Fields.Add(new EmbedFieldBuilder().WithName(user2Gives[i].name).WithIsInline(true));
			} else {
				b.Fields.Add(new EmbedFieldBuilder().WithName("-").WithIsInline(true));
			}
		}

		return b.Build();
	}
}