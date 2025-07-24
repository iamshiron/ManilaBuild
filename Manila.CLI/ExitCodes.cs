
public static class ExitCodes {
    public static readonly int SUCCESS = 0x00;

    // Exception exit codes
    public static readonly int SCRIPTING_ERROR = 0x01;
    public static readonly int BUILD_ERROR = 0x02;
    public static readonly int CONFIGURATION_ERROR = 0x03;
    public static readonly int RUNTIME_ERROR = 0x04;
    public static readonly int PLUGIN_ERROR = 0x05;

    public static readonly int ANY_KNOWN_ERROR = 0x0e;
    public static readonly int UNKNOWN_ERROR = 0x0f;

    // Command exit codes
    public static readonly int USER_COMMAND_ERROR = 0x10;
}
