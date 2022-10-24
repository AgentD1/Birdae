using Discord;
using Discord.WebSocket;

namespace Birdae;

#pragma warning disable CS8618
public class Family {
	public string name, scientificName;
	public int id;
	public List<Bird> birds;

	public async Task SendFamilyMessage(SocketSlashCommand command) {
		EmbedBuilder b = new EmbedBuilder {
			Title = name,
			Url = $"https://en.wikipedia.org/w/index.php?title=Special:Search&search={System.Net.WebUtility.UrlEncode(scientificName)}",
			Description = scientificName,
			Fields = new List<EmbedFieldBuilder>(),
		};

		Bird.AppendBirdList(b, birds);

		await Paginator.SendPagination(command, b);
	}
}