using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;

namespace Birdae;

public class Paginator {
	public List<EmbedFieldBuilder> elements;
	public SocketUser user;
	public int currentPage;

	public static ConcurrentDictionary<string, Paginator> paginatorsById = new();

	public static async Task HandlePaginatorRequest(SocketMessageComponent smc) {
		string id = smc.Data.CustomId[1..^1];
		bool forward = smc.Data.CustomId[^1] == 'f';

		if (paginatorsById.TryGetValue(id, out Paginator paginator)) {
			int lastPageNumber = (int)Math.Ceiling(paginator.elements.Count / (double)Settings.instance.pageSize);

			paginator.currentPage = Math.Clamp(paginator.currentPage + (forward ? 1 : -1), 1, lastPageNumber);

			Embed originalEmbed = smc.Message.Embeds.First();

			EmbedBuilder newBuilder = originalEmbed.ToEmbedBuilder();

			newBuilder.Fields.Clear();

			foreach (var field in paginator.elements.Skip(Settings.instance.pageSize * (paginator.currentPage - 1)).Take(Settings.instance.pageSize)) {
				newBuilder.Fields.Add(field);
			}

			newBuilder.Footer.Text = $"Page {paginator.currentPage}/{Math.Ceiling(paginator.elements.Count / (double)Settings.instance.pageSize)}";

			ComponentBuilder b = new ComponentBuilder();

			b.AddRow(new ActionRowBuilder().WithButton(ButtonBuilder.CreatePrimaryButton("<", $"b{id}p").WithDisabled(paginator.currentPage == 1))
				.WithButton(ButtonBuilder.CreatePrimaryButton(">", $"b{id}f").WithDisabled(paginator.currentPage == lastPageNumber)));

			await smc.UpdateAsync(msg => {
				msg.Embeds = new[] { newBuilder.Build() };
				msg.Components = b.Build();
			}, RequestOptions.Default);
		} else {
			await smc.UpdateAsync(msg => { msg.Components = new ComponentBuilder().Build(); }, RequestOptions.Default);
		}
	}

	public static async Task SendPagination(SocketSlashCommand command, EmbedBuilder embed) {
		string randomId = Guid.NewGuid().ToString();

		Paginator paginator = new Paginator {
			currentPage = 1,
			elements = new(embed.Fields),
			user = command.User
		};

		paginatorsById.TryAdd(randomId, paginator);

		embed.Footer ??= new EmbedFooterBuilder();

		embed.Footer.Text = $"Page {paginator.currentPage}/{Math.Ceiling(paginator.elements.Count / (double)Settings.instance.pageSize)}";

		embed.Fields ??= new List<EmbedFieldBuilder>();
		embed.Fields.Clear();

		foreach (var field in paginator.elements.Skip(Settings.instance.pageSize * (paginator.currentPage - 1)).Take(Settings.instance.pageSize)) {
			embed.Fields.Add(field);
		}

		await command.RespondAsync("", new[] { embed.Build() }, components: ComponentBuilder.FromComponents(new[] {
			new ActionRowBuilder().WithButton(ButtonBuilder.CreatePrimaryButton("<", $"b{randomId}p").WithDisabled(true))
				.WithButton(ButtonBuilder.CreatePrimaryButton(">", $"b{randomId}f").WithDisabled(paginator.elements.Count < Settings.instance.pageSize)).Build()
		}).Build());
	}
}