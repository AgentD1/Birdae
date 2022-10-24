namespace Birdae; 

public struct JsonBirdDatabase {
	public Family[] families;
	public Bird[] birds;

	public struct Family {
		public string name, scientificName;
		public int id;
		public int[] birds;
	}

	public struct Bird {
		public string name, scientificName;
		public int id;
		public int familyId;
		public string imageId, discordImageId;
	}
}