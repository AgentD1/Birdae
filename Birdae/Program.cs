using FuzzySharp;
using FuzzySharp.Extractor;

namespace Birdae;

using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;

public class Program {
	public static Task Main(string[] args) => new Program().MainAsync();

	public static DiscordSocketClient client = new();
	public static BirdDatabase birdDb;

	public async Task MainAsync() {
		client.Log += Log;
		string token = await File.ReadAllTextAsync("token.txt");

		Task parseBirdDatabase = ParseBirdDatabase();

		await client.LoginAsync(TokenType.Bot, token);
		await client.StartAsync();

		client.Ready += ClientReady;

		client.SlashCommandExecuted += SlashCommandHandler;

		client.ButtonExecuted += ButtonHandler;

		await parseBirdDatabase;

		Console.WriteLine(birdDb.birdsById[256].name);

		Settings.instance = new Settings {
			pageSize = 10,
			rollNumber = 12,
			rollRestoreTime = TimeSpan.FromMinutes(1),
			claimRestoreTime = TimeSpan.FromMinutes(1),
		};

		await Task.Delay(-1);
	}

	private Task Log(LogMessage message) {
		Console.WriteLine(message.ToString());
		return Task.CompletedTask;
	}

	public async Task ParseBirdDatabase() {
		string fileContents = await File.ReadAllTextAsync("birds.json");
		await Task.Run(() => {
			JsonBirdDatabase db = JsonConvert.DeserializeObject<JsonBirdDatabase>(fileContents);
			birdDb = new BirdDatabase(db);
		});
	}

	public async Task ClientReady() {
		var globalCommand = new SlashCommandBuilder();
		globalCommand.WithName("ping");
		globalCommand.WithDescription("Returns your ping to the bot");

		await client.CreateGlobalApplicationCommandAsync(globalCommand.Build());

		List<SlashCommandBuilder> guildCommands = new List<SlashCommandBuilder>();

		var infoCommand = new SlashCommandBuilder();
		infoCommand.WithName("info");
		infoCommand.WithDescription("Get the info of a bird, based on its common name or scientific name");
		var infoCommandOption = new SlashCommandOptionBuilder();
		infoCommandOption.IsRequired = true;
		infoCommandOption.Name = "species";
		infoCommandOption.Description = "Common or Scientific Name";
		infoCommandOption.Type = ApplicationCommandOptionType.String;
		infoCommand.AddOption(infoCommandOption);
		guildCommands.Add(infoCommand);

		var familyInfoCommand = new SlashCommandBuilder {
			Name = "family-info",
			Description = "Get the info of a family based on its common name or scientific name",
			Options = new List<SlashCommandOptionBuilder> {
				new() {
					IsRequired = true,
					Name = "family",
					Description = "Common or Scientific Name",
					Type = ApplicationCommandOptionType.String,
				},
			}
		};
		guildCommands.Add(familyInfoCommand);

		var buttonCommand = new SlashCommandBuilder {
			Name = "button",
			Description = "Make a button for testing purposes",
			Options = new List<SlashCommandOptionBuilder> {
				new() {
					IsRequired = true,
					Name = "thing",
					Description = "The thing you want the button to say or do",
					Type = ApplicationCommandOptionType.String
				}
			}
		};
		guildCommands.Add(buttonCommand);

		var aviaryCommand = new SlashCommandBuilder {
			Name = "aviary",
			Description = "Aviary-related Commands",
			Options = new List<SlashCommandOptionBuilder> {
				new() {
					Name = "create",
					Description = "Create an aviary",
					Type = ApplicationCommandOptionType.SubCommand,
					Options = new List<SlashCommandOptionBuilder> {
						new() {
							Name = "name",
							Description = "The name of your aviary",
							IsRequired = true,
							MinLength = 3,
							MaxLength = 50,
							Type = ApplicationCommandOptionType.String
						},
					}
				},
				new() {
					Name = "display",
					Description = "Display a user's aviary, or your own",
					Type = ApplicationCommandOptionType.SubCommand,
					Options = new List<SlashCommandOptionBuilder> {
						new() {
							Name = "user",
							Description = "The user to display",
							Type = ApplicationCommandOptionType.User,
							IsRequired = false,
						}
					}
				},
				new() {
					Name = "rename",
					Description = "Rename your aviary",
					Type = ApplicationCommandOptionType.SubCommand,
					Options = new List<SlashCommandOptionBuilder> {
						new() {
							Name = "name",
							Description = "The new name for your aviary",
							Type = ApplicationCommandOptionType.String,
							IsRequired = true,
							MinLength = 3,
							MaxLength = 50,
						}
					}
				},
				new() {
					Name = "claim",
					Description = "Add a bird to your aviary",
					Type = ApplicationCommandOptionType.SubCommand,
					Options = new List<SlashCommandOptionBuilder> {
						new() {
							Name = "name",
							Description = "The name of the bird",
							Type = ApplicationCommandOptionType.String,
							IsRequired = true,
							MinLength = 3,
							MaxLength = 50,
						}
					}
				},
				new() {
					Name = "give",
					Description = "Give a bird from your aviary to someone else",
					Type = ApplicationCommandOptionType.SubCommand,
					Options = new List<SlashCommandOptionBuilder> {
						new() {
							Name = "bird-name",
							Description = "The name of the bird you want to give",
							Type = ApplicationCommandOptionType.String,
							IsRequired = true,
							MinLength = 3,
							MaxLength = 50,
						},
						new() {
							Name = "user",
							Description = "The user you want to give the bird to",
							Type = ApplicationCommandOptionType.User,
							IsRequired = true,
						},
					}
				},
			}
		};
		guildCommands.Add(aviaryCommand);

		var saveCommand = new SlashCommandBuilder {
			Name = "save",
			Description = "Save the state of Birdae. For administrator use only",
		};
		guildCommands.Add(saveCommand);

		var loadCommand = new SlashCommandBuilder {
			Name = "load",
			Description = "Load the state of Birdae. For administrator use only",
		};
		guildCommands.Add(loadCommand);

		var rollCommand = new SlashCommandBuilder {
			Name = "roll",
			Description = "Roll to find and claim birds",
		};
		guildCommands.Add(rollCommand);

		foreach (var guild in client.Guilds) {
			foreach (var command in guildCommands) {
				Console.WriteLine($"Now processing {command.Name}");
				await guild.CreateApplicationCommandAsync(command.Build());
			}
		}

		Console.WriteLine("My ready isn't blocking it");
	}

	public async Task ButtonHandler(SocketMessageComponent component) {
		if (component.Data.CustomId.StartsWith("b")) {
			await Paginator.HandlePaginatorRequest(component);
			return;
		}

		if (component.Data.CustomId.StartsWith("r")) {
			await RollModule.HandleClaimRequest(component);
			return;
		}

		await component.RespondAsync($"{component.User.Mention} says {component.Data.CustomId}");
	}

	public async Task SlashCommandHandler(SocketSlashCommand command) {
		if (command.CommandName == "ping") {
			await command.RespondAsync($"Ping is {-(DateTimeOffset.Now - command.CreatedAt).Milliseconds} ms");
			return;
		}

		if (command.CommandName == "info") {
			string birdName = (string)command.Data.Options.First().Value;

			if (birdDb.birds.TryGetValue(birdName.ToLower(), out Bird bird)) {
				await command.RespondAsync("", embed: bird.GenerateBirdEmbed());
				return;
			}

			Bird bestBird = birdDb.FindBirdByFuzzyName(birdName);

			await command.RespondAsync("Closest match: ", embed: bestBird.GenerateBirdEmbed());
			return;
		}

		if (command.CommandName == "family-info") {
			string familyName = (string)command.Data.Options.First(o => o.Name == "family").Value;

			if (birdDb.families.TryGetValue(familyName.ToLower(), out Family family)) {
				await family.SendFamilyMessage(command);
				return;
			}

			ExtractedResult<string> foundFamily =
				Process.ExtractOne(familyName, birdDb.familiesById.Select(f => f.name));
			ExtractedResult<string> foundScientificFamily =
				Process.ExtractOne(familyName, birdDb.familiesById.Select(f => f.scientificName));

			Family bestFamily;
			if (foundFamily.Score > foundScientificFamily.Score) {
				bestFamily = birdDb.familiesById[foundFamily.Index];
			} else {
				bestFamily = birdDb.familiesById[foundScientificFamily.Index];
			}

			await bestFamily.SendFamilyMessage(command);
			return;
		}

		if (command.CommandName == "button") {
			ComponentBuilder b = new();

			b.WithButton(label: "Hello", (string)command.Data.Options.First());

			await command.RespondAsync("Here's a button bozo: ", components: b.Build());
			return;
		}

		if (command.CommandName == "aviary") {
			string fieldName = command.Data.Options.First().Name;
			switch (fieldName) {
				case "display": {
					await Aviary.DisplayAviaryCommand(command);
					return;
				}
				case "create": {
					string name = (string)command.Data.Options.First().Options.First().Value;
					await command.RespondAsync("", new[] {
						Aviary.CreateAviaryCommand(command.User, command.GuildId ?? throw new Exception("Blank guild ID"), name)
					});
					return;
				}
				case "rename": {
					string name = (string)command.Data.Options.First().Options.First().Value;
					await command.RespondAsync($"This is rename, with the argument {name}");
					return;
				}
				case "claim": {
					string name = (string)command.Data.Options.First().Options.First().Value;
					bool success = Aviary.AddBirdToAviary(command.User, command.GuildId ?? 0, birdDb.FindBirdByFuzzyName(name));
					if (!success) {
						await command.RespondAsync("Something went wrong!");
						return;
					}

					await command.RespondAsync("Added :grin:");
					return;
				}
				case "give": {
					string name = (string)command.Data.Options.First().Options.First(x => x.Name == "bird-name").Value;
					SocketUser receiver = (SocketUser)command.Data.Options.First().Options.First(x => x.Name == "user").Value;

					await command.RespondAsync(receiver.Mention, new[] {
						Aviary.GiveBird(command.User, receiver, command.GuildId ?? throw new Exception("Blank guild ID"), birdDb.FindBirdByFuzzyName(name))
					});
					return;
				}
			}

			await command.RespondAsync($"Some stupid stuff {fieldName}");
		}

		if (command.CommandName == "save") {
			await command.RespondAsync("Saving...");
			JsonManager.SerializeStateToFile();
			await command.ModifyOriginalResponseAsync(p => p.Content = "Save complete!");
			return;
		}

		if (command.CommandName == "load") {
			await command.RespondAsync("Loading...");
			JsonManager.LoadStateFromFile();
			await command.ModifyOriginalResponseAsync(p => p.Content = "Load complete!");
			return;
		}

		if (command.CommandName == "roll") {
			await RollModule.RollCommand(command);
			return;
		}


		await command.RespondAsync("This command hasn't been implemented yet!");
	}
}