namespace Shiron.Manila.Exceptions;

public class ContextException : Exception {
	public readonly Context cIs;
	public readonly Context cShould;

	public ContextException(Context cIs, Context cShould) : base("Wrong Context! Is: " + cIs + ", Should: " + cShould) {
		this.cIs = cIs;
		this.cShould = cShould;
	}
}


public enum Context {
	PROJECT,
	WORKSPACE
}
