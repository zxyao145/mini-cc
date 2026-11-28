import template from '../../../deploy/docker-compose.ghcr.sqlite.yaml?raw';

const version = import.meta.env.PUBLIC_MINICC_RELEASE_TAG || 'vX.Y.Z';

export const composeDownloadPath = '/downloads/docker-compose.yaml';
export const quickstartCompose = template.replaceAll('${MINICC_VERSION:?Set MINICC_VERSION to the Release tag}', version);
