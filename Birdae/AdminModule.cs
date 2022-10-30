using Discord;
using Discord.WebSocket;

namespace Birdae;

public static class AdminModule {
	public static async Task UpdateAllGuildCommands(SocketSlashCommand? command) {
		if (command != null) {
			SocketGuildUser usr = (SocketGuildUser)command.User;
			if (usr.Roles.All(x => x.Name.ToLower() != "admin")) {
				await command.RespondAsync("You do not have permission to do this!");
				return;
			}
			await command.RespondAsync("Creating commands...");
		}

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

		List<SlashCommandBuilder> adminGuildCommands = new List<SlashCommandBuilder>();

		var updateAllCommandsCommand = new SlashCommandBuilder {
			Name = "update-all-commands",
			Description = "Update all slash commands in all guilds",
		};
		adminGuildCommands.Add(updateAllCommandsCommand);


		if (command != null) {
			await command.ModifyOriginalResponseAsync(x => x.Content = $"Applying to guild {0}/{Program.client.Guilds.Count}");
		}

		int guildNum = 1;

		try {
			foreach (var guild in Program.client.Guilds) {
				if (guildNum % 10 == 0) {
					await command!.ModifyOriginalResponseAsync(x => x.Content = $"Applying to guild {guildNum}/{Program.client.Guilds.Count}");
				}

				if (guild.Id == Settings.instance.adminGuildId) {
					await guild.BulkOverwriteApplicationCommandAsync(guildCommands.Concat(adminGuildCommands)
						.Select(x => (ApplicationCommandProperties)x.Build()).ToArray());
				} else {
					await guild.BulkOverwriteApplicationCommandAsync(guildCommands.Select(x => (ApplicationCommandProperties)x.Build()).ToArray());
				}

				if (command != null)
					guildNum++;
			}
		} catch (Exception) {
			if (command != null)
				await command.ModifyOriginalResponseAsync(x => x.Content = "Update failed! See console for more details.");
			throw;
		}

		if (command != null)
			await command.ModifyOriginalResponseAsync(x => x.Content = "Command update complete!");
	}
}