using FuzzySharp;
using FuzzySharp.Extractor;

#pragma warning disable CS8618
namespace Birdae;

public class BirdDatabase {
	public JsonBirdDatabase rawDatabase;

	public List<Bird> birdsById = new();
	public List<Family> familiesById = new();
	public Dictionary<string, Bird> birds = new();
	public Dictionary<string, Family> families = new();

	public BirdDatabase(JsonBirdDatabase db) {
		rawDatabase = db;

		foreach (var f in db.families) {
			Family family = new Family {
				birds = new List<Bird>(f.birds.Length),
				id = f.id,
				name = f.name,
				scientificName = f.scientificName
			};

			familiesById.Add(family);
			families.Add(family.name.ToLower(), family);
		}

		foreach (var b in db.birds) {
			Bird bird = new Bird {
				name = b.name,
				id = b.id,
				imageId = b.imageId,
				scientificName = b.scientificName,
				discordImageId = b.discordImageId,
				family = familiesById[b.familyId]
			};

			birdsById.Add(bird);
			birds.Add(bird.name.ToLower(), bird);

			bird.family.birds.Add(bird);
		}
	}

	public Bird FindBirdByFuzzyName(string name) {
		ExtractedResult<string> foundBird = Process.ExtractOne(name, birdsById.Select(b => b.name));
		ExtractedResult<string> foundScientificBird = Process.ExtractOne(name, birdsById.Select(b => b.scientificName));

		Bird bestBird;
		if (foundBird.Score > foundScientificBird.Score) {
			bestBird = birdsById[foundBird.Index];
		} else {
			bestBird = birdsById[foundScientificBird.Index];
		}

		return bestBird;
	}
}