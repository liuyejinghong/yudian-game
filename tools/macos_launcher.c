/* Finder entry for the local playable export; engine CLI remains Yudian. */
#include <mach-o/dyld.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>

int main(void) {
    char path[PATH_MAX], resolved[PATH_MAX];
    uint32_t size = sizeof(path);
    if (_NSGetExecutablePath(path, &size) || !realpath(path, resolved)) return 126;
    char *name = strrchr(resolved, '/');
    if (!name) return 126;
    size_t available = sizeof(resolved) - (size_t)(++name - resolved);
    if (snprintf(name, available, "Yudian") >= (int)available) return 126;
    execl(resolved, resolved, "--", "--live-terrain", (char *)NULL);
    perror("余电启动失败");
    return 126;
}
