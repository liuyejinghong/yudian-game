/* 余电原生验收包装器：只清除继承的 YUDIAN_* 变量并设置私有测试环境，然后 exec 候选引擎。
 * 不修改引擎、焦点、身份或信任状态；路径由 prepare.py 通过 -D 注入（绝对路径，已在 prepare.py 校验）。 */
#include <crt_externs.h>
#include <limits.h>
#include <mach-o/dyld.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

#ifndef ACC_ENGINE_NAME
#define ACC_ENGINE_NAME "Yudian"
#endif
#ifndef ACC_SLOT_ROOT
#error "ACC_SLOT_ROOT 必须由 prepare.py 通过 -D 注入"
#endif
#ifndef ACC_TEST_SAVE
#error "ACC_TEST_SAVE 必须由 prepare.py 通过 -D 注入"
#endif
#ifndef ACC_GUI_SIZE
#error "ACC_GUI_SIZE 必须由 prepare.py 通过 -D 注入"
#endif
#ifndef ACC_CAPTURE_DIR
#error "ACC_CAPTURE_DIR 必须由 prepare.py 通过 -D 注入"
#endif
#ifndef ACC_LOG_FILE
#error "ACC_LOG_FILE 必须由 prepare.py 通过 -D 注入"
#endif

#define EXIT_SETUP 126

/* 清除本进程继承的全部 YUDIAN_* 变量；只影响即将 exec 的子进程，不改父进程或系统环境。 */
static int clear_inherited_yudian_env(void) {
    char ***envp = _NSGetEnviron();
    if (envp == NULL) return -1;
    int removed = 1;
    while (removed) {
        removed = 0;
        for (char **entry = *envp; entry != NULL && *entry != NULL; ++entry) {
            if (strncmp(*entry, "YUDIAN_", 7) != 0) continue;
            const char *eq = strchr(*entry, '=');
            size_t length = eq != NULL ? (size_t)(eq - *entry) : strlen(*entry);
            char name[256];
            if (length == 0 || length >= sizeof(name)) return -1;
            memcpy(name, *entry, length);
            name[length] = '\0';
            if (unsetenv(name) != 0) return -1;
            removed = 1;
            break; /* unsetenv 会移动 environ，必须从头重新扫描 */
        }
    }
    return 0;
}

int main(void) {
    char path[PATH_MAX];
    char resolved[PATH_MAX];
    char engine[PATH_MAX];
    uint32_t size = sizeof(path);
    if (_NSGetExecutablePath(path, &size) != 0 || realpath(path, resolved) == NULL) return EXIT_SETUP;
    char *slash = strrchr(resolved, '/');
    if (slash == NULL) return EXIT_SETUP;
    *slash = '\0'; /* 现在 resolved 为 .../Contents/MacOS */
    if (snprintf(engine, sizeof(engine), "%s/%s", resolved, ACC_ENGINE_NAME) >= (int)sizeof(engine)) return EXIT_SETUP;
    if (access(engine, X_OK) != 0) {
        perror("余电验收引擎不可执行");
        return EXIT_SETUP;
    }
    if (clear_inherited_yudian_env() != 0
        || setenv("YUDIAN_PLAYER_GUI_TEST", "1", 1) != 0
        || setenv("YUDIAN_TUTORIAL_TEST_ROOT", ACC_SLOT_ROOT, 1) != 0
        || setenv("YUDIAN_PLAYER_TEST_SAVE", ACC_TEST_SAVE, 1) != 0
        || setenv("YUDIAN_PLAYER_GUI_SIZE", ACC_GUI_SIZE, 1) != 0
        || setenv("YUDIAN_PLAYER_CAPTURE_DIR", ACC_CAPTURE_DIR, 1) != 0) {
        perror("余电验收环境设置失败");
        return EXIT_SETUP;
    }
    execl(engine, engine, "--rendering-driver", "metal", "--accessibility", "always",
          "--max-fps", "60", "--log-file", ACC_LOG_FILE, (char *)NULL);
    perror("余电验收引擎启动失败");
    return EXIT_SETUP;
}
