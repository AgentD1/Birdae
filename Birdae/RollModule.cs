using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Serialization;

namespace Birdae;

public static class RollModule {
	static Random random = new();

	public static TimeSpan expiry = TimeSpan.FromSeconds(60);

	static ConcurrentDictionary<string, Claim> activeClaims = new();

	public static async Task HandleClaimRequest(SocketMessageComponent smc) {
		string id = smc.Data.CustomId[1..];
		ulong guildId = smc.GuildId ?? throw new Exception("Empty guild id");

		if (activeClaims.TryGetValue(id, out Claim claim)) {
			if (claim.expiry < DateTimeOffset.Now) {
				activeClaims.TryRemove(id, out _);
				await smc.UpdateAsync(properties => properties.Components = new ComponentBuilder().Build());
				await smc.FollowupAsync("This bird is no longer claimable!");
				return;
			}

			if (!Aviary.guildAvailableBirds.TryGetValue(guildId, out ConcurrentDictionary<Bird, byte> availableBirds) ||
			    !Aviary.guildAndUserToAviaryDict[guildId].ContainsKey(smc.User.Id)) {
				await smc.RespondAsync($"{smc.User.Mention}, You do not have an aviary yet! Do /aviary create [name] to make one!");
				return;
			}

			User userStatus = User.guildAndIdToUser[guildId][smc.User.Id];

			if (!userStatus.CanClaim()) {
				await smc.RespondAsync($"{smc.User.Mention}, you cannot currently claim! " +
				                       $"Your claims will be restored in {(userStatus.claimRestoreTime - DateTimeOffset.Now).TotalMinutes:F2} minutes");
				return;
			}

			Aviary userAviary = Aviary.guildAndUserToAviaryDict[guildId][smc.User.Id];

			bool success = true;

			lock (userAviary)
			lock (availableBirds) {
				if (availableBirds.TryRemove(claim.bird, out _)) {
					userAviary.birds.Add(claim.bird);
				} else {
					success = false;
				}
			}

			if (success) {
				userStatus.Claim();
				activeClaims.TryRemove(id, out _);
				await smc.UpdateAsync(properties => properties.Components = new ComponentBuilder().Build());
				await smc.FollowupAsync($"{smc.User.Mention}, You have claimed this bird successfully! :grin:");
			} else {
				activeClaims.TryRemove(id, out _);
				await smc.UpdateAsync(properties => properties.Components = new ComponentBuilder().Build());
				await smc.FollowupAsync("This bird is no longer claimable!");
			}
		} else {
			await smc.UpdateAsync(properties => properties.Components = new ComponentBuilder().Build());
			await smc.FollowupAsync("This bird is no longer claimable!");
		}
	}

	public static async Task RollCommand(SocketSlashCommand command) {
		ulong guildId = command.GuildId ?? throw new Exception("Empty guild id");

		if (!Aviary.guildAvailableBirds.TryGetValue(guildId, out ConcurrentDictionary<Bird, byte> availableBirds) ||
		    !Aviary.guildAndUserToAviaryDict[guildId].ContainsKey(command.User.Id)) {
			await command.RespondAsync("You do not have an aviary yet! Do /aviary create [name] to make one!");
			return;
		}

		User userStatus = User.guildAndIdToUser[guildId][command.User.Id];

		if (!userStatus.CanRoll()) {
			await command.RespondAsync($"You can't roll right now! Your rolls will be restored in " +
			                           $"{(userStatus.rollRestoreTime - DateTimeOffset.Now).TotalMinutes:F2} minutes!");
			return;
		}

		Bird bird;

		lock (availableBirds)
		lock (random) {
			bird = availableBirds.Keys.Skip(random.Next(availableBirds.Keys.Count)).First();
		}

		string guid = Guid.NewGuid().ToString();

		ComponentBuilder builder = new ComponentBuilder().WithButton(ButtonBuilder.CreatePrimaryButton("Claim", $"r{guid}"));

		Claim claim = new Claim(DateTimeOffset.Now + expiry, bird);
		activeClaims.TryAdd(guid, claim);

		userStatus.Roll();

		await command.RespondAsync("You rolled: ", new[] { bird.GenerateBirdEmbed() }, components: builder.Build());
	}

	class Claim {
		public DateTimeOffset expiry;
		public Bird bird;

		public Claim(DateTimeOffset expiry, Bird bird) {
			this.expiry = expiry;
			this.bird = bird;
		}
	}
}