import java.nio.charset.StandardCharsets;
import java.nio.file.*;

// Small deterministic processor fixture, compiled for Java 8 by the test runner.
public class Probe {
    public static void main(String[] args) throws Exception {
        System.out.println("WORK=" + System.getProperty("user.dir"));
        if (args[0].equals("fail")) {
            System.err.println("fixture processor failed");
            System.exit(7);
        }
        if (args[0].equals("sleep")) Thread.sleep(10000);
        if (args[0].equals("write")) {
            Files.write(Paths.get(args[1]), "ok\n".getBytes(StandardCharsets.UTF_8));
            if (args.length > 2) Files.write(Paths.get(args[2]), "run\n".getBytes(StandardCharsets.UTF_8),
                StandardOpenOption.CREATE, StandardOpenOption.APPEND);
        }
    }
}
