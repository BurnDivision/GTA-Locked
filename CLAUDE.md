# Notes for Claude sessions

## Where to push: GitLab first, then GitHub

This project's primary home is GitLab: https://gitlab.com/BurnDivisionGames/gta-locked
(private). GitHub (`origin`) is kept as the second copy because cloud sessions
clone from it.

Once per session, add the GitLab remote:

    git remote add gitlab https://gitlab.com/BurnDivisionGames/gta-locked.git

Every time you push a branch, push to GitLab first, then GitHub:

    git push -u gitlab <branch>
    git push -u origin <branch>

The GitLab token is stored in the cloud environment's network secrets and the
proxy adds it for gitlab.com, so the plain URL above needs no credentials.
Never put a token in a remote URL or commit one. If the GitLab push fails with
"HTTP Basic: Access denied", the token has expired or been revoked: tell the
user, and still push to GitHub so no work is lost.
