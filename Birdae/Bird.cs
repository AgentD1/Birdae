using Discord;

namespace Birdae;

#pragma warning disable CS8618
public class Bird {
	public string name, scientificName;
	public int id;
	public Family family;
	public string imageId, discordImageId;

	public Embed GenerateBirdEmbed() {
		EmbedBuilder b = new EmbedBuilder {
			Title = name,
			Url = $"https://en.wikipedia.org/w/index.php?title=Special:Search&search={System.Net.WebUtility.UrlEncode(scientificName)}",
			Description = scientificName,
			Footer = new EmbedFooterBuilder {
				Text = family.name
			},
			ImageUrl = discordImageId
		};

		return b.Build();
	}

	public static void AppendBirdList(EmbedBuilder builder, IEnumerable<Bird> birdList) {
		foreach (var bird in birdList) {
			builder.Fields.Add(new EmbedFieldBuilder().WithName(bird.name).WithValue(bird.scientificName));
		}
	}
}