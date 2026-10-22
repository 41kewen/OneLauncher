package onelauncher.experiment;

import java.lang.reflect.*;
import java.nio.file.*;
import java.util.function.Function;
import __MOD_PACKAGE__.Mod;

/** Experiment-only mod: uses a fresh test instance and never opens a user world. */
@Mod("olan_probe")
public final class ProbeMod {
    private static final String WORLD = "OneLauncher-Probe-World";
    private static int state;
    private static long entered;
    private static Path worldFile;

    public ProbeMod() {
        marker("MOD_LOADED");
        Thread worker = new Thread(() -> {
            try {
                Class<?> type = Class.forName("net.minecraft.client.Minecraft");
                Method get = type.getMethod("getInstance");
                Method execute = type.getMethod("execute", Runnable.class);
                for (;;) {
                    Thread.sleep(500);
                    Object minecraft = get.invoke(null);
                    if (minecraft != null) execute.invoke(minecraft, (Runnable) () -> tick(minecraft));
                }
            } catch (Throwable error) { fail(error); }
        }, "OneLauncher experiment observer");
        worker.setDaemon(true);
        worker.start();
    }

    private static void tick(Object minecraft) {
        if (state < 0) return;
        try {
            Class<?> type = minecraft.getClass();
            Object screen = type.getField("screen").get(minecraft);
            boolean menu = screen != null && screen.getClass().getName().equals("net.minecraft.client.gui.screens.TitleScreen");
            Object overlay = type.getMethod("getOverlay").invoke(minecraft);
            if (state == 0 && menu && overlay == null) {
                marker("MENU_READY");
                state = 1;
                entered = System.currentTimeMillis();
            } else if (state == 1 && System.currentTimeMillis() - entered > 3000) {
                Path instance = ((java.io.File) type.getField("gameDirectory").get(minecraft)).toPath();
                worldFile = instance.resolve("saves").resolve(WORLD).resolve("level.dat");
                Object flows = type.getMethod("createWorldOpenFlows").invoke(minecraft);
                if (Files.isRegularFile(worldFile)) {
                    marker("WORLD_REOPEN_REQUESTED");
                    flows.getClass().getMethod("openWorld", String.class, Runnable.class)
                        .invoke(flows, WORLD, (Runnable) () -> fail(new IllegalStateException("World reopen canceled")));
                } else {
                    marker("WORLD_CREATE_REQUESTED");
                    Class<?> settings = Class.forName("net.minecraft.world.level.LevelSettings");
                    Class<?> mode = Class.forName("net.minecraft.world.level.GameType");
                    Class<?> difficulty = Class.forName("net.minecraft.world.Difficulty");
                    Class<?> rules = Class.forName("net.minecraft.world.level.GameRules");
                    Class<?> data = Class.forName("net.minecraft.world.level.WorldDataConfiguration");
                    Object levelSettings = settings.getConstructor(String.class, mode, boolean.class, difficulty,
                        boolean.class, rules, data).newInstance(WORLD, mode.getField("CREATIVE").get(null), false,
                        difficulty.getField("PEACEFUL").get(null), true, rules.getConstructor().newInstance(), data.getField("DEFAULT").get(null));
                    Class<?> options = Class.forName("net.minecraft.world.level.levelgen.WorldOptions");
                    Object worldOptions = options.getConstructor(long.class, boolean.class, boolean.class).newInstance(10152026L, false, false);
                    Class<?> presets = Class.forName("net.minecraft.world.level.levelgen.presets.WorldPresets");
                    Method dimensions = presets.getMethod("createNormalWorldDimensions", Class.forName("net.minecraft.core.RegistryAccess"));
                    Function<Object, Object> factory = registry -> {
                        try { return dimensions.invoke(null, registry); }
                        catch (ReflectiveOperationException error) { throw new RuntimeException(error); }
                    };
                    flows.getClass().getMethod("createFreshLevel", String.class, settings, options, Function.class,
                        Class.forName("net.minecraft.client.gui.screens.Screen"))
                        .invoke(flows, WORLD, levelSettings, worldOptions, factory, screen);
                }
                state = 2;
            } else if (state == 2 && type.getField("level").get(minecraft) != null && type.getField("player").get(minecraft) != null && overlay == null) {
                marker("WORLD_READY");
                state = 3;
                entered = System.currentTimeMillis();
            } else if (state == 3 && System.currentTimeMillis() - entered > 10000) {
                Object title = Class.forName("net.minecraft.client.gui.screens.TitleScreen").getConstructor().newInstance();
                type.getMethod("disconnect", Class.forName("net.minecraft.client.gui.screens.Screen")).invoke(minecraft, title);
                state = 4;
                entered = System.currentTimeMillis();
            } else if (state == 4 && menu && overlay == null && System.currentTimeMillis() - entered > 3000) {
                if (!Files.isRegularFile(worldFile)) throw new IllegalStateException("Missing saved level.dat");
                marker("WORLD_SAVED");
                marker("EXIT_REQUESTED");
                state = -1;
                type.getMethod("stop").invoke(minecraft);
            }
        } catch (Throwable error) { fail(error); }
    }

    private static void marker(String value) { System.out.println("[OLAN_PROBE] " + value); }
    private static void fail(Throwable error) {
        state = -1;
        marker("PROBE_FAILED " + error);
        error.printStackTrace();
    }
}
