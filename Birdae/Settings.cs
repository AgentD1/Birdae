namespace Birdae;

public class Settings {
	public static Settings instance;

	public int pageSize;
	public int rollNumber;
	public TimeSpan rollRestoreTime;
	public TimeSpan claimRestoreTime;
}